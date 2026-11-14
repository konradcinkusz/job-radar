using JobRadar.Core.Ports;
using JobRadar.Sources.Http;

namespace JobRadar.Sources.Tests.HttpTests;

public sealed class FixtureSourceHttpTests
{
    [Fact]
    public async Task Fixture_http_fails_on_unmapped_url()
    {
        var http = new FixtureSourceHttp("unused-folder", new Dictionary<string, string>());
        var url = new Uri("https://nowhere.test/missing");

        InvalidOperationException getError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => http.GetAsync(url, CancellationToken.None));
        InvalidOperationException postError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => http.PostAsync(url, "application/json", "{}", CancellationToken.None));

        Assert.Contains(url.ToString(), getError.Message, StringComparison.Ordinal);
        Assert.Contains(url.ToString(), postError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fixture_http_serves_the_mapped_file_with_its_content_type()
    {
        string folder = Path.Combine(Path.GetTempPath(), "jr-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "jobs.json"), "{\"ok\":true}");
            File.WriteAllText(Path.Combine(folder, "feed.rss"), "<rss/>");
            File.WriteAllText(Path.Combine(folder, "page.html"), "<html/>");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "plain");
            File.WriteAllText(Path.Combine(folder, "search.json"), "[]");

            var map = new Dictionary<string, string>
            {
                ["https://a.test/jobs.json"] = "jobs.json",
                ["https://a.test/feed"] = "feed.rss",
                ["https://a.test/page"] = "page.html",
                ["https://a.test/notes"] = "notes.txt",
                ["POST https://a.test/search"] = "search.json",
            };
            var http = new FixtureSourceHttp(folder, map);

            HttpResult json = await http.GetAsync(new Uri("https://a.test/jobs.json"), CancellationToken.None);
            HttpResult feed = await http.GetAsync(new Uri("https://a.test/feed"), CancellationToken.None);
            HttpResult page = await http.GetAsync(new Uri("https://a.test/page"), CancellationToken.None);
            HttpResult notes = await http.GetAsync(new Uri("https://a.test/notes"), CancellationToken.None);
            HttpResult post = await http.PostAsync(new Uri("https://a.test/search"), "application/json", "{}", CancellationToken.None);
            HttpResult postFallback = await http.PostAsync(new Uri("https://a.test/jobs.json"), "application/json", "{}", CancellationToken.None);

            Assert.Equal(200, json.Status);
            Assert.Equal("application/json", json.ContentType);
            Assert.Equal("{\"ok\":true}", json.Body);
            Assert.Equal("application/rss+xml", feed.ContentType);
            Assert.Equal("text/html", page.ContentType);
            Assert.Equal("text/plain", notes.ContentType);
            Assert.Equal("[]", post.Body); // "POST " + url is looked up first
            Assert.Equal("{\"ok\":true}", postFallback.Body); // then the plain url
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
