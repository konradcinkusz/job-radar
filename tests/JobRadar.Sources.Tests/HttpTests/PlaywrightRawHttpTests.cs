using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using JobRadar.Core.Ports;
using JobRadar.Sources.Http;

namespace JobRadar.Sources.Tests.HttpTests;

public sealed class PlaywrightRawHttpTests
{
    private const string UserAgent = "JobRadar/1.0 (personal use)";
    private static readonly TimeSpan GuardLimit = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Get_returns_status_body_and_content_type()
    {
        using var server = new LoopbackServer(_ => (200, "application/json; charset=utf-8", "{\"ok\":true}", false));
        await using PlaywrightRawHttp http = await PlaywrightRawHttp.CreateAsync();

        HttpResult result = await http.SendAsync(Request(HttpMethodKind.Get, server.Url("/jobs.json")), CancellationToken.None);

        Assert.Equal(200, result.Status);
        Assert.Equal("{\"ok\":true}", result.Body);
        Assert.Equal("application/json; charset=utf-8", result.ContentType);
        Assert.Equal("application/json; charset=utf-8", result.Headers["content-type"]);
        Assert.Equal("GET", server.Last!.Method);
        Assert.Equal("/jobs.json", server.Last.Path);
    }

    [Fact]
    public async Task Post_sends_the_body_and_content_type()
    {
        using var server = new LoopbackServer(_ => (200, "application/json", "[]", false));
        await using PlaywrightRawHttp http = await PlaywrightRawHttp.CreateAsync();
        const string body = "{\"rawSearch\":\"backend\",\"page\":1}";

        HttpResult result = await http.SendAsync(
            Request(HttpMethodKind.Post, server.Url("/search"), "application/infiniteSearch+json", body), CancellationToken.None);

        Assert.Equal(200, result.Status);
        Assert.Equal("POST", server.Last!.Method);
        Assert.Equal("application/infiniteSearch+json", server.Last.ContentType);
        Assert.Equal(body, server.Last.Body);
    }

    [Fact]
    public async Task Non_success_status_is_a_result_not_an_exception()
    {
        using var server = new LoopbackServer(req => req.Url?.AbsolutePath == "/unavailable"
            ? (503, "text/plain", "busy", false)
            : (404, "text/plain", "gone", false));
        await using PlaywrightRawHttp http = await PlaywrightRawHttp.CreateAsync();

        HttpResult unavailable = await http.SendAsync(Request(HttpMethodKind.Get, server.Url("/unavailable")), CancellationToken.None);
        HttpResult missing = await http.SendAsync(Request(HttpMethodKind.Get, server.Url("/missing")), CancellationToken.None);

        Assert.Equal(503, unavailable.Status);
        Assert.Equal("busy", unavailable.Body);
        Assert.Equal(404, missing.Status);
        Assert.Equal("gone", missing.Body);
    }

    [Fact]
    public async Task User_agent_is_sent()
    {
        using var server = new LoopbackServer(_ => (200, "text/plain", "ok", false));
        await using PlaywrightRawHttp http = await PlaywrightRawHttp.CreateAsync();

        await http.SendAsync(Request(HttpMethodKind.Get, server.Url("/ua")), CancellationToken.None);

        Assert.Equal(UserAgent, server.Last!.UserAgent);
    }

    [Fact]
    public async Task Timeout_is_applied()
    {
        using var server = new LoopbackServer(_ => (200, "text/plain", "late", true));
        await using PlaywrightRawHttp http = await PlaywrightRawHttp.CreateAsync();
        var stopwatch = Stopwatch.StartNew();

        Task<HttpResult> send = http.SendAsync(Request(HttpMethodKind.Get, server.Url("/slow"), timeout: TimeSpan.FromSeconds(1)), CancellationToken.None);
        Task finished = await Task.WhenAny(send, Task.Delay(GuardLimit));

        Assert.True(ReferenceEquals(send, finished), $"SendAsync did not throw within {GuardLimit.TotalSeconds} s: the timeout was not applied.");
        await Assert.ThrowsAnyAsync<Exception>(() => send);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900), $"SendAsync failed after {stopwatch.Elapsed.TotalMilliseconds} ms, before the 1 s timeout.");
    }

    [Fact]
    public async Task Transport_failure_throws()
    {
        int closedPort = FreePort();
        await using PlaywrightRawHttp http = await PlaywrightRawHttp.CreateAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            http.SendAsync(Request(HttpMethodKind.Get, new Uri($"http://127.0.0.1:{closedPort}/closed")), CancellationToken.None));
    }

    private static RawHttpRequest Request(HttpMethodKind method, Uri url, string? contentType = null, string? body = null, TimeSpan? timeout = null) =>
        new(method, url, contentType, body, UserAgent, timeout ?? TimeSpan.FromSeconds(10));

    /// <summary>A port that was free a moment ago. Bind-then-release is racy in theory; loopback tests here accept that.</summary>
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    private sealed record RecordedRequest(string Method, string Path, string? UserAgent, string? ContentType, string Body);

    /// <summary>
    /// An in-process HTTP server on 127.0.0.1 for one test. The handler answers each request; when it asks for a delay, the answer
    /// waits until <see cref="Dispose"/> releases it (no fixed sleep). Only the last request is recorded.
    /// </summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Func<HttpListenerRequest, (int Status, string ContentType, string Body, bool Delay)> _handler;
        private readonly ManualResetEventSlim _release = new(false);
        private readonly Task _loop;
        private readonly int _port;
        private volatile RecordedRequest? _last;

        public LoopbackServer(Func<HttpListenerRequest, (int Status, string ContentType, string Body, bool Delay)> handler)
        {
            _handler = handler;
            _port = FreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _loop = Task.Factory.StartNew(Serve, TaskCreationOptions.LongRunning);
        }

        public RecordedRequest? Last => _last;

        public Uri Url(string path) => new($"http://127.0.0.1:{_port}{path}");

        private void Serve()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    return; // stopped by Dispose
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                Handle(context);
            }
        }

        private void Handle(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            _last = new RecordedRequest(request.HttpMethod, request.Url?.AbsolutePath ?? string.Empty, request.UserAgent, request.ContentType, body);
            var (status, contentType, text, delay) = _handler(request);
            if (delay)
            {
                _release.Wait(TimeSpan.FromSeconds(30));
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                context.Response.StatusCode = status;
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
            {
                // The client gave up (the timeout test) before the delayed answer: nothing to deliver.
            }
        }

        public void Dispose()
        {
            _release.Set();
            _listener.Stop();
            _loop.Wait(TimeSpan.FromSeconds(10));
            _listener.Close();
            _release.Dispose();
        }
    }
}
