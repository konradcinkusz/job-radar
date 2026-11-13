using JobRadar.Cli.Commands;
using JobRadar.Cli.Wiring;
using JobRadar.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddConfig().AddSources().AddStore().AddAgents().AddCommands();

using var host = builder.Build();
var commands = host.Services.GetServices<ICliCommand>().ToDictionary(c => c.Name, StringComparer.Ordinal);

if (args.Length == 0 || !commands.TryGetValue(args[0], out var command))
{
    Console.Error.WriteLine($"usage: jobradar <{string.Join('|', commands.Keys.Order())}>");
    return 2;
}

return await command.RunAsync(args[1..], CancellationToken.None);
