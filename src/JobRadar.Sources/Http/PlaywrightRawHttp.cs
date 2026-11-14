using JobRadar.Core.Ports;
using Microsoft.Playwright;

namespace JobRadar.Sources.Http;

/// <summary>
/// The real transport (SPEC §6.3, ADR-0002): HTTP through Playwright's <see cref="IAPIRequestContext"/>, no browser is launched.
/// One attempt per call: no retry, no delay (those live in <see cref="PolicySourceHttp"/>, R11).
/// </summary>
public sealed class PlaywrightRawHttp : IRawHttp, IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IAPIRequestContext _context;

    private PlaywrightRawHttp(IPlaywright playwright, IAPIRequestContext context)
    {
        _playwright = playwright;
        _context = context;
    }

    public static async Task<PlaywrightRawHttp> CreateAsync()
    {
        IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        try
        {
            IAPIRequestContext context = await playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
            return new PlaywrightRawHttp(playwright, context);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    public async Task<HttpResult> SendAsync(RawHttpRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        bool isPost = request.Method == HttpMethodKind.Post;
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["User-Agent"] = request.UserAgent,
        };
        if (isPost && request.ContentType is not null)
        {
            headers["Content-Type"] = request.ContentType;
        }

        var options = new APIRequestContextOptions
        {
            Timeout = (float)request.Timeout.TotalMilliseconds,
            Headers = headers,
            FailOnStatusCode = false,
            DataString = isPost ? request.Body : null,
        };

        string url = request.Url.AbsoluteUri;
        IAPIResponse response = isPost
            ? await _context.PostAsync(url, options).ConfigureAwait(false)
            : await _context.GetAsync(url, options).ConfigureAwait(false);
        try
        {
            var responseHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> header in response.Headers)
            {
                responseHeaders[header.Key.ToLowerInvariant()] = header.Value;
            }

            string contentType = responseHeaders.TryGetValue("content-type", out string? value) ? value : string.Empty;
            string body = await response.TextAsync().ConfigureAwait(false);
            return new HttpResult(response.Status, contentType, body, responseHeaders);
        }
        finally
        {
            await response.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync().ConfigureAwait(false);
        _playwright.Dispose();
    }
}
