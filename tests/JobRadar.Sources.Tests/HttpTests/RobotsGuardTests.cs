using System.Net.Http;
using JobRadar.Core.Ports;
using JobRadar.Sources.Http;

namespace JobRadar.Sources.Tests.HttpTests;

public sealed class RobotsGuardTests
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();
    private static readonly string[] RobotsA = ["https://a.test/robots.txt"];
    private static readonly string[] RobotsAB = ["https://a.test/robots.txt", "https://b.test/robots.txt"];

    [Fact]
    public async Task Robots_longest_match_wins()
    {
        var http = new FakeSourceHttp(_ => Ok("User-agent: *\nDisallow: /jobs\nAllow: /jobs/public\n"));
        var guard = new RobotsGuard();

        Assert.True(await guard.IsAllowedAsync(new Uri("https://jobs.test/jobs/public/1"), http));
        Assert.False(await guard.IsAllowedAsync(new Uri("https://jobs.test/jobs/private"), http));
    }

    [Fact]
    public async Task Robots_unreachable_allows()
    {
        var guard = new RobotsGuard();
        var throwing = new FakeSourceHttp(_ => Task.FromException<HttpResult>(new HttpRequestException("unreachable")));
        var notFound = new FakeSourceHttp(_ => Task.FromResult(new HttpResult(404, "text/html", "not found", NoHeaders)));

        Assert.True(await guard.IsAllowedAsync(new Uri("https://down.test/jobs/private"), throwing));
        Assert.True(await guard.IsAllowedAsync(new Uri("https://missing.test/jobs/private"), notFound));
    }

    [Fact]
    public async Task Robots_is_fetched_once_per_host()
    {
        var http = new FakeSourceHttp(_ => Ok("User-agent: *\nDisallow: /private\n"));
        var guard = new RobotsGuard();

        Assert.True(await guard.IsAllowedAsync(new Uri("https://a.test/jobs/1"), http));
        Assert.False(await guard.IsAllowedAsync(new Uri("https://a.test/private/1"), http));
        Assert.True(await guard.IsAllowedAsync(new Uri("https://a.test/jobs/2"), http));
        Assert.Equal(RobotsA, http.Requests);

        Assert.True(await guard.IsAllowedAsync(new Uri("https://b.test/jobs/1"), http));
        Assert.Equal(RobotsAB, http.Requests);
    }

    [Fact]
    public async Task Robots_only_the_star_group_counts()
    {
        const string body = """
            # comments are ignored
            User-agent: Googlebot
            Disallow: /

            User-agent: Bingbot
            User-agent: *
            Disallow: /secret # trailing comment
            Disallow:
            """;
        var http = new FakeSourceHttp(_ => Ok(body));
        var guard = new RobotsGuard();

        Assert.True(await guard.IsAllowedAsync(new Uri("https://c.test/jobs"), http));
        Assert.False(await guard.IsAllowedAsync(new Uri("https://c.test/secret/1"), http));
    }

    private static Task<HttpResult> Ok(string body) => Task.FromResult(new HttpResult(200, "text/plain", body, NoHeaders));

    /// <summary>Answers robots.txt requests from a delegate and records every url requested.</summary>
    private sealed class FakeSourceHttp : ISourceHttp
    {
        private readonly Func<Uri, Task<HttpResult>> _respond;
        private readonly List<string> _requests = new();

        public FakeSourceHttp(Func<Uri, Task<HttpResult>> respond) => _respond = respond;

        public IReadOnlyList<string> Requests => _requests;

        public Task<HttpResult> GetAsync(Uri url, CancellationToken ct)
        {
            _requests.Add(url.ToString());
            return _respond(url);
        }

        public Task<HttpResult> PostAsync(Uri url, string contentType, string body, CancellationToken ct) =>
            Task.FromException<HttpResult>(new InvalidOperationException("the robots guard never POSTs"));
    }
}
