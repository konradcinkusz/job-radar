using System.Reflection;

namespace JobRadar.Core.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Core_references_no_assembly_outside_the_framework()
    {
        var core = typeof(CoreMarker).Assembly;
        var offenders = core.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && n is not "mscorlib" and not "netstandard")
            .ToList();
        Assert.Empty(offenders);
    }
}
