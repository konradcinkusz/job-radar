using JobRadar.Cli.Commands;
using JobRadar.Cli.Wiring;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Cli.Tests;

public class CommandWiringTests
{
    [Fact]
    public async Task AddCommands_registers_doctor_by_convention_and_it_exits_zero()
    {
        var provider = new ServiceCollection().AddCommands().BuildServiceProvider();
        var doctor = provider.GetServices<ICliCommand>().Single(c => c.Name == "doctor");
        Assert.Equal(0, await doctor.RunAsync([], CancellationToken.None));
    }

    [Fact]
    public void AddCommands_registers_nothing_that_is_not_a_command()
    {
        var provider = new ServiceCollection().AddCommands().BuildServiceProvider();
        Assert.All(provider.GetServices<ICliCommand>(), c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
    }
}
