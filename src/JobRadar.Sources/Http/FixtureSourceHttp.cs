using JobRadar.Core.Ports;

namespace JobRadar.Sources.Http;

/// <summary>
/// Replays recorded responses from a folder. <paramref name="map"/> is url → file name inside <paramref name="folder"/>.
/// An unmapped url throws <see cref="InvalidOperationException"/> naming the url: never a fake 404. No delay, no counting.
/// </summary>
public sealed class FixtureSourceHttp : ISourceHttp
{
    private readonly string _folder;
    private readonly IReadOnlyDictionary<string, string> _map;

    public FixtureSourceHttp(string folder, IReadOnlyDictionary<string, string> map)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        _map = map ?? throw new ArgumentNullException(nameof(map));
    }

    public Task<HttpResult> GetAsync(Uri url, CancellationToken ct) => Task.FromResult(Serve(url, url.ToString()));

    /// <summary>Looks up <c>"POST " + url</c> first, then the plain url.</summary>
    public Task<HttpResult> PostAsync(Uri url, string contentType, string body, CancellationToken ct)
    {
        string postKey = "POST " + url;
        return Task.FromResult(Serve(url, _map.ContainsKey(postKey) ? postKey : url.ToString()));
    }

    private HttpResult Serve(Uri url, string key)
    {
        if (!_map.TryGetValue(key, out string? file))
        {
            throw new InvalidOperationException($"fixture: no file mapped for {url}");
        }

        string text = File.ReadAllText(Path.Combine(_folder, file));
        return new HttpResult(200, ContentTypeOf(file), text, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static string ContentTypeOf(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".json" => "application/json",
        ".xml" or ".rss" => "application/rss+xml",
        ".html" => "text/html",
        _ => "text/plain",
    };
}
