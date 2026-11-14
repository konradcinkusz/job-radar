using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.ExceptionServices;
using JobRadar.Core.Ports;

namespace JobRadar.Sources.Http;

/// <summary>
/// The HTTP policy of SPEC R9 and R11, in one place (SPEC §6.3):
/// a random 1–3 s delay before each request to a host after the first; at most three hosts in flight; calls to one host run one
/// after another; a User-Agent; a 30 s timeout per request; GET only is retried (2 retries, back-off 2 s then 8 s, honouring
/// <c>Retry-After</c> up to 60 s) on status 429, 5xx and transport exceptions; a POST is never retried; a request counter per host.
/// </summary>
// CA1001: the global SemaphoreSlim is owned for the life of the policy (one source run). It holds no unmanaged handle unless
// AvailableWaitHandle is used, and this class never uses it, so there is nothing to dispose.
#pragma warning disable CA1001
public sealed class PolicySourceHttp : ISourceHttp
#pragma warning restore CA1001
{
    public const string UserAgent = "JobRadar/1.0 (personal use)";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private const int MaxHosts = 3;
    private const int MaxAttempts = 3; // one attempt and two retries (R11)
    private const int RetryAfterCapSeconds = 60;
    private static readonly TimeSpan[] BackOff = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)];

    private readonly IRawHttp _inner;
    private readonly IDelay _delay;
    private readonly Func<double> _random;
    private readonly ISourceLog? _log;
    private readonly SemaphoreSlim _globalGate = new(MaxHosts, MaxHosts);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostGates = new(StringComparer.Ordinal); // one single-permit gate per host
    private readonly object _state = new();
    private readonly HashSet<string> _calledHosts = new(StringComparer.Ordinal); // guarded by _state
    private readonly Dictionary<string, int> _requestCounts = new(StringComparer.Ordinal); // guarded by _state
    private int _totalRequests; // guarded by _state

    /// <param name="random">Returns a value in 0..1; the delay is <c>1 + 2 * random()</c> seconds.</param>
    public PolicySourceHttp(IRawHttp inner, IDelay delay, Func<double> random, ISourceLog? log = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _log = log;
    }

    public Task<HttpResult> GetAsync(Uri url, CancellationToken ct) =>
        SendAsync(HttpMethodKind.Get, url, contentType: null, body: null, ct);

    public Task<HttpResult> PostAsync(Uri url, string contentType, string body, CancellationToken ct) =>
        SendAsync(HttpMethodKind.Post, url, contentType, body, ct);

    /// <summary>Attempts made to <paramref name="host"/>, retries included.</summary>
    public int RequestCount(string host)
    {
        lock (_state)
        {
            return _requestCounts.TryGetValue(NormalizeHost(host), out int count) ? count : 0;
        }
    }

    /// <summary>Attempts made to all hosts, retries included.</summary>
    public int TotalRequests
    {
        get
        {
            lock (_state)
            {
                return _totalRequests;
            }
        }
    }

    private async Task<HttpResult> SendAsync(HttpMethodKind method, Uri url, string? contentType, string? body, CancellationToken ct)
    {
        string host = NormalizeHost(url.Host);

        // Host slot first, then the global slot: a call waiting for its host does not hold one of the three global slots.
        SemaphoreSlim hostGate = _hostGates.GetOrAdd(host, static _ => new SemaphoreSlim(1, 1));
        await hostGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _globalGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!TryMarkFirstCall(host))
                {
                    await _delay.DelayAsync(TimeSpan.FromSeconds(1 + 2 * _random()), ct).ConfigureAwait(false);
                }

                return await AttemptsAsync(method, url, host, contentType, body, ct).ConfigureAwait(false);
            }
            finally
            {
                _globalGate.Release();
            }
        }
        finally
        {
            hostGate.Release();
        }
    }

    private async Task<HttpResult> AttemptsAsync(HttpMethodKind method, Uri url, string host, string? contentType, string? body, CancellationToken ct)
    {
        // A POST is a write that is not idempotent: exactly one attempt (R11).
        int maxAttempts = method == HttpMethodKind.Get ? MaxAttempts : 1;

        for (int attempt = 1; ; attempt++)
        {
            HttpResult? result = null;
            ExceptionDispatchInfo? failure = null;
            try
            {
                result = await SendOnceAsync(method, url, host, contentType, body, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // any transport failure other than the caller's cancellation is a retryable failure (R11)
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
#pragma warning restore CA1031

            bool retryable = failure is not null || IsRetryableStatus(result!.Status);
            if (!retryable || attempt >= maxAttempts)
            {
                failure?.Throw();
                return result!;
            }

            TimeSpan wait = RetryAfter(result) ?? BackOff[attempt - 1];
            string reason = failure is not null ? $"transport error {failure.SourceException.GetType().Name}" : $"HTTP {result!.Status}";
            _log?.Warn($"{host}: attempt {attempt} failed ({reason}), retrying after {wait.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s");
            await _delay.DelayAsync(wait, ct).ConfigureAwait(false);
        }
    }

    private Task<HttpResult> SendOnceAsync(HttpMethodKind method, Uri url, string host, string? contentType, string? body, CancellationToken ct)
    {
        lock (_state)
        {
            _totalRequests++;
            _requestCounts[host] = (_requestCounts.TryGetValue(host, out int count) ? count : 0) + 1;
        }

        return _inner.SendAsync(new RawHttpRequest(method, url, contentType, body, UserAgent, RequestTimeout), ct);
    }

    /// <summary>True the first time a host is seen; the caller delays on every later call to that host.</summary>
    private bool TryMarkFirstCall(string host)
    {
        lock (_state)
        {
            return _calledHosts.Add(host);
        }
    }

    private static bool IsRetryableStatus(int status) => status == 429 || status >= 500;

    /// <summary>The <c>Retry-After</c> wait in seconds (integer form only), capped at 60 s; null when absent or not an integer.</summary>
    private static TimeSpan? RetryAfter(HttpResult? result)
    {
        if (result is null)
        {
            return null;
        }

        foreach (KeyValuePair<string, string> header in result.Headers)
        {
            if (string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(header.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int seconds))
            {
                return TimeSpan.FromSeconds(Math.Min(seconds, RetryAfterCapSeconds));
            }
        }

        return null;
    }

    private static string NormalizeHost(string host) => host.ToLowerInvariant();
}
