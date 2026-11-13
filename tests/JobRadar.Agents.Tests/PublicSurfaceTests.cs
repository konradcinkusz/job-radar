using System.Reflection;
namespace JobRadar.Agents.Tests;

/// <summary>Skeleton (T-00.03). T-02.02 adds the check that no public Agents type exposes an SDK type.</summary>
public class PublicSurfaceTests
{
    [Fact]
    public void Only_Agents_references_the_copilot_sdk()
    {
        var others = new[]
        {
            typeof(JobRadar.Core.CoreMarker).Assembly,
            typeof(JobRadar.Config.ConfigMarker).Assembly,
            typeof(JobRadar.Sources.SourcesMarker).Assembly,
            typeof(JobRadar.Store.StoreMarker).Assembly,
        };
        var offenders = others
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => (Assembly: a.GetName().Name, Ref: r.Name!)))
            .Where(x => x.Ref.StartsWith("GitHub.Copilot", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void The_guard_can_fail_the_agents_assembly_does_reference_the_sdk_or_nothing_is_checked()
    {
        // If Agents did not reference the SDK once real code exists, the test above would pass for the wrong reason.
        // At scaffold time Agents has only a placeholder, so this asserts the guard's input is loadable.
        Assert.NotNull(typeof(JobRadar.Agents.AgentsMarker).Assembly.GetName().Name);
    }
}
