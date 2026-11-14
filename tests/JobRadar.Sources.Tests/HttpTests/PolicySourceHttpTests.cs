using System.Net.Http;
using System.Threading;
using JobRadar.Core.Ports;
using JobRadar.Sources.Http;

namespace JobRadar.Sources.Tests.HttpTests;

public sealed class PolicySourceHttpTests
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();
    private static readonly Uri HostA = new("https://a.test/jobs");
    private static readonly Uri HostB = new("https://b.test/jobs");
    private static readonly string[] ExpectedDelayTrace = ["send a.test", "delay 2", "send a.test", "send b.test"];

    [Fact]
    public async Task Retries_on_503_twice_then_fails()
    {
        var raw = new ScriptedRawHttp();
        raw.Respond(503);
        raw.Respond(503);
        raw.Respond(503);
        var delay = new RecordingDelay();
        var http = new PolicySourceHttp(raw, delay, () => 0.5);

        HttpResult result = await http.GetAsync(HostA, CancellationToken.None);

        Assert.Equal(503, result.Status);
        Assert.Equal(3, raw.Requests.Length);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8) }, delay.Delays);
    }

    [Fact]
    public async Task Does_not_retry_on_404()
    {
        var raw = new ScriptedRawHttp();
        raw.Respond(404);
        var delay = new RecordingDelay();
        var http = new PolicySourceHttp(raw, delay, () => 0.5);

        HttpResult result = await http.GetAsync(HostA, CancellationToken.None);

        Assert.Equal(404, result.Status);
        Assert.Single(raw.Requests);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task Honours_retry_after_up_to_the_cap()
    {
        var raw = new ScriptedRawHttp();
        raw.Respond(429, Headers(("Retry-After", "5")));
        raw.Respond(200);
        raw.Respond(503, Headers(("Retry-After", "600")));
        raw.Respond(200);
        var delay = new RecordingDelay();
        var http = new PolicySourceHttp(raw, delay, () => 0.5);

        HttpResult first = await http.GetAsync(HostA, CancellationToken.None);
        HttpResult second = await http.GetAsync(HostB, CancellationToken.None);

        Assert.Equal(200, first.Status);
        Assert.Equal(200, second.Status);
        // Host A waited 5 s as asked; host B asked for 600 s and waited the 60 s cap. Neither is a between-request delay.
        Assert.Equal(new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60) }, delay.Delays);
        Assert.Equal(4, raw.Requests.Length);
    }

    [Fact]
    public async Task Post_is_never_retried()
    {
        var raw = new ScriptedRawHttp();
        raw.Respond(503);
        raw.Fail(new HttpRequestException("connection reset"));
        var delay = new RecordingDelay();
        var http = new PolicySourceHttp(raw, delay, () => 0.5);

        HttpResult result = await http.PostAsync(HostA, "application/json", "{}", CancellationToken.None);
        await Assert.ThrowsAsync<HttpRequestException>(() => http.PostAsync(HostB, "application/json", "{}", CancellationToken.None));

        Assert.Equal(503, result.Status);
        Assert.Equal(2, raw.Requests.Length);
        Assert.All(raw.Requests, request => Assert.Equal(HttpMethodKind.Post, request.Method));
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task Delay_is_applied_between_requests_to_one_host()
    {
        var trace = new List<string>();
        var raw = new ScriptedRawHttp(trace);
        raw.Respond(200);
        raw.Respond(200);
        raw.Respond(200);
        var delay = new RecordingDelay(trace);
        var http = new PolicySourceHttp(raw, delay, () => 0.5); // 1 + 2 * 0.5 = 2 s

        await http.GetAsync(HostA, CancellationToken.None);
        await http.GetAsync(HostA, CancellationToken.None);
        await http.GetAsync(HostB, CancellationToken.None);

        // No delay before the first request to a host; one 2 s delay before the second request to host a; none before the first to b.
        Assert.Equal(ExpectedDelayTrace, trace);
    }

    [Fact]
    public async Task Never_more_than_three_hosts_at_once()
    {
        using var raw = new BlockingRawHttp();
        var http = new PolicySourceHttp(raw, new RecordingDelay(), () => 0.5);

        var calls = new List<Task<HttpResult>>();
        for (int i = 0; i < 6; i++)
        {
            calls.Add(http.GetAsync(new Uri($"https://host{i}.test/"), CancellationToken.None));
        }

        for (int i = 0; i < 3; i++)
        {
            Assert.True(await raw.Entered.WaitAsync(TimeSpan.FromSeconds(10)), "a call did not reach the transport within 10 s");
        }

        Assert.Equal(3, raw.InFlight);
        Assert.Equal(3, raw.MaxInFlight);
        Assert.Equal(3, raw.Started); // the other three calls are still waiting for a host slot

        raw.ReleaseAll();
        await Task.WhenAll(calls);

        Assert.Equal(6, raw.Started);
        Assert.Equal(3, raw.MaxInFlight);
    }

    [Fact]
    public async Task Request_counter_counts_attempts_per_host()
    {
        var raw = new ScriptedRawHttp();
        raw.Respond(503);
        raw.Respond(503);
        raw.Respond(200);
        raw.Respond(200);
        var http = new PolicySourceHttp(raw, new RecordingDelay(), () => 0.5);

        await http.GetAsync(HostA, CancellationToken.None);
        await http.GetAsync(HostB, CancellationToken.None);

        Assert.Equal(3, http.RequestCount("a.test"));
        Assert.Equal(3, http.RequestCount("A.TEST"));
        Assert.Equal(1, http.RequestCount("b.test"));
        Assert.Equal(0, http.RequestCount("c.test"));
        Assert.Equal(4, http.TotalRequests);
    }

    [Fact]
    public async Task Exception_from_transport_is_retried_then_rethrown()
    {
        var raw = new ScriptedRawHttp();
        raw.Fail(new HttpRequestException("connection reset"));
        raw.Fail(new HttpRequestException("connection reset"));
        raw.Fail(new HttpRequestException("connection reset"));
        var delay = new RecordingDelay();
        var http = new PolicySourceHttp(raw, delay, () => 0.5);

        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(HostA, CancellationToken.None));

        Assert.Equal(3, raw.Requests.Length);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8) }, delay.Delays);
    }

    private static Dictionary<string, string> Headers(params (string Name, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Name, pair => pair.Value);

    /// <summary>Answers each call with the next scripted result or exception; records every request; fails loudly when the script runs out.</summary>
    private sealed class ScriptedRawHttp : IRawHttp
    {
        private readonly object _gate = new();
        private readonly Queue<(HttpResult? Result, Exception? Error)> _script = new();
        private readonly List<RawHttpRequest> _requests = new();
        private readonly List<string>? _trace;

        public ScriptedRawHttp(List<string>? trace = null) => _trace = trace;

        public RawHttpRequest[] Requests
        {
            get
            {
                lock (_gate)
                {
                    return _requests.ToArray();
                }
            }
        }

        public void Respond(int status, IReadOnlyDictionary<string, string>? headers = null)
        {
            lock (_gate)
            {
                _script.Enqueue((new HttpResult(status, "text/plain", string.Empty, headers ?? NoHeaders), null));
            }
        }

        public void Fail(Exception error)
        {
            lock (_gate)
            {
                _script.Enqueue((null, error));
            }
        }

        public Task<HttpResult> SendAsync(RawHttpRequest request, CancellationToken ct)
        {
            (HttpResult? Result, Exception? Error) step;
            lock (_gate)
            {
                _requests.Add(request);
                _trace?.Add($"send {request.Url.Host}");
                if (_script.Count == 0)
                {
                    return Task.FromException<HttpResult>(new InvalidOperationException($"script exhausted for {request.Url}"));
                }

                step = _script.Dequeue();
            }

            return step.Error is not null
                ? Task.FromException<HttpResult>(step.Error)
                : Task.FromResult(step.Result!);
        }
    }

    /// <summary>Holds every call until <see cref="ReleaseAll"/>; tracks how many are in flight at once.</summary>
    private sealed class BlockingRawHttp : IRawHttp, IDisposable
    {
        private readonly TaskCompletionSource<HttpResult> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _entered = new(0);
        private int _inFlight;
        private int _maxInFlight;
        private int _started;

        /// <summary>Released once for every call that reaches the transport.</summary>
        public SemaphoreSlim Entered => _entered;

        public void Dispose() => _entered.Dispose();

        public int InFlight => Volatile.Read(ref _inFlight);

        public int MaxInFlight => Volatile.Read(ref _maxInFlight);

        public int Started => Volatile.Read(ref _started);

        public void ReleaseAll() => _release.TrySetResult(new HttpResult(200, "text/plain", string.Empty, NoHeaders));

        public async Task<HttpResult> SendAsync(RawHttpRequest request, CancellationToken ct)
        {
            int now = Interlocked.Increment(ref _inFlight);
            RecordMax(now);
            Interlocked.Increment(ref _started);
            _entered.Release();
            try
            {
                return await _release.Task.WaitAsync(ct);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        private void RecordMax(int now)
        {
            int observed = Volatile.Read(ref _maxInFlight);
            while (now > observed)
            {
                int prior = Interlocked.CompareExchange(ref _maxInFlight, now, observed);
                if (prior == observed)
                {
                    return;
                }

                observed = prior;
            }
        }
    }

    /// <summary>Records every requested delay and returns at once; optionally appends to a shared trace.</summary>
    private sealed class RecordingDelay : IDelay
    {
        private readonly List<string>? _trace;

        public RecordingDelay(List<string>? trace = null) => _trace = trace;

        public List<TimeSpan> Delays { get; } = new();

        public Task DelayAsync(TimeSpan delay, CancellationToken ct)
        {
            Delays.Add(delay);
            _trace?.Add($"delay {(int)delay.TotalSeconds}");
            return Task.CompletedTask;
        }
    }
}
