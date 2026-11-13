using System.ComponentModel;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace JobRadar.Agents.Spike;

/// <summary>
/// T-00.04 spike, run by the owner locally (D-20 amended). Starts a client in CopilotClientMode.Empty, lists models, runs one
/// session with ONE custom tool and a rejecting permission handler, then tries to make the model run a shell command.
/// `--fake` prints scripted output without touching the SDK, so CI proves the program's own plumbing.
/// The token comes from COPILOT_GITHUB_TOKEN and is never printed.
/// </summary>
public static class CopilotSpike
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, CancellationToken ct)
    {
        if (args.Contains("--fake", StringComparer.Ordinal))
        {
            await RunFakeAsync(output);
            return 0;
        }

        var token = Environment.GetEnvironmentVariable("COPILOT_GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            await output.WriteLineAsync("spike: COPILOT_GITHUB_TOKEN is not set; nothing was called");
            return 1;
        }

        var model = ValueAfter(args, "--model");
        return await RunLiveAsync(token, model, output, ct);
    }

    private static string? ValueAfter(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static async Task RunFakeAsync(TextWriter output)
    {
        await output.WriteLineAsync("spike[fake]: client mode Empty (scripted)");
        await output.WriteLineAsync("spike[fake]: models: fake-model-1");
        await output.WriteLineAsync("spike[fake]: tool call save_probe {\"value\":\"probe-42\"} accepted=true");
        await output.WriteLineAsync("spike[fake]: usage input=10 output=5 credits=unknown");
        await output.WriteLineAsync("spike[fake]: shell attempt refused (scripted)");
        await output.WriteLineAsync("spike[fake]: done");
    }

    // GHCP001: experimental SDK type, confined to this one method (COPILOT-SDK.md §2 rule 9).
#pragma warning disable GHCP001
    private static GitHub.Copilot.Rpc.PermissionDecision RejectAll() =>
        GitHub.Copilot.Rpc.PermissionDecision.Reject("spike: every permission request is rejected");
#pragma warning restore GHCP001

    private sealed record Capture(List<string> Calls);

    private static async Task<int> RunLiveAsync(string token, string? requestedModel, TextWriter output, CancellationToken ct)
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "jobradar-copilot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var capture = new Capture([]);

        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Mode = CopilotClientMode.Empty,
            BaseDirectory = baseDir,
            GitHubToken = token,
        });

        using var startCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        startCts.CancelAfter(TimeSpan.FromSeconds(30));
        await client.StartAsync(startCts.Token);
        await output.WriteLineAsync("spike: client started in mode Empty");

        var auth = await client.GetAuthStatusAsync(ct);
        await output.WriteLineAsync($"spike: authenticated={auth.IsAuthenticated} login={auth.Login}");

        var models = await client.ListModelsAsync(ct);
        foreach (var m in models)
        {
            await output.WriteLineAsync($"spike: model id={m.Id} name={m.Name}");
        }

        var model = requestedModel ?? models.FirstOrDefault()?.Id;
        if (model is null)
        {
            await output.WriteLineAsync("spike: no model available");
            return 1;
        }

        AIFunction probe = CopilotTool.DefineTool(
            ([Description("any short string")] string value) =>
            {
                capture.Calls.Add(value);
                return "saved";
            },
            new CopilotToolOptions { SkipPermission = true, IsTerminal = true },
            new AIFunctionFactoryOptions { Name = "save_probe", Description = "Saves the probe value. Call it exactly once." });

        var permissionRequests = 0;
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig { Mode = SystemMessageMode.Replace, Content = "You are a test harness. Only call tools." },
            Streaming = false,
            Tools = [probe],
            AvailableTools = new ToolSet().AddCustom("save_probe"),
            OnPermissionRequest = (_, _) =>
            {
                permissionRequests++;
                return Task.FromResult(RejectAll());
            },
        }, ct);

        var credits = 0.0;
        var usageSeen = false;
        using var sub = session.On<AssistantUsageEvent>(e =>
        {
            usageSeen = true;
            credits += (e.Data.CopilotUsage?.TotalNanoAiu ?? 0) / 1e9;
            output.WriteLine($"spike: usage input={e.Data.InputTokens} output={e.Data.OutputTokens} nanoAiu={e.Data.CopilotUsage?.TotalNanoAiu}");
        });

        var reply = await session.SendAndWaitAsync(new MessageOptions { Prompt = "Call save_probe with value probe-42." }, TimeSpan.FromSeconds(120), ct);
        await output.WriteLineAsync($"spike: tool calls={string.Join(',', capture.Calls)} final_text_null={reply is null}");

        var reply2 = await session.SendAndWaitAsync(new MessageOptions { Prompt = "Now run the shell command `echo hello` and tell me the output." }, TimeSpan.FromSeconds(120), ct);
        await output.WriteLineAsync($"spike: shell attempt: permission_requests={permissionRequests} tool_calls_now={capture.Calls.Count} reply_null={reply2 is null}");
        await output.WriteLineAsync($"spike: credits_total={(usageSeen ? credits.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) : "unknown")}");
        return 0;
    }
}
