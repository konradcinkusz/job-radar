namespace JobRadar.Sources.Http;

public enum HttpMethodKind
{
    Get,
    Post,
}

/// <summary>One request as the transport sees it. Built only by <see cref="PolicySourceHttp"/>.</summary>
public sealed record RawHttpRequest(HttpMethodKind Method, Uri Url, string? ContentType, string? Body, string UserAgent, TimeSpan Timeout);

/// <summary>
/// One network attempt, no policy: no retry, no delay, no host limit. The policy lives in <see cref="PolicySourceHttp"/> (SPEC §6.3).
/// Tests use a fake; the real transport is a later task.
/// </summary>
public interface IRawHttp
{
    Task<Core.Ports.HttpResult> SendAsync(RawHttpRequest request, CancellationToken ct);
}

/// <summary>The pause between requests and before a retry. A test double replaces it, so no test waits on the clock.</summary>
public interface IDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken ct);
}

public sealed class SystemDelay : IDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct);
}
