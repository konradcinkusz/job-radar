using System.Reflection;
using JobRadar.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Cli.Wiring;

/// <summary>
/// Wiring lives in Cli because Config, Sources, Store and Agents may not take a DI package (SPEC §3.2 dependency budget).
/// Registration is by convention so implementers never edit wiring (TASKS "Rules").
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfig(this IServiceCollection services) => services;

    // T-01.01 adds the IJobSource scan of the Sources assembly here.
    public static IServiceCollection AddSources(this IServiceCollection services) => services;

    // T-01.01 adds the repository scan of the Store assembly here.
    public static IServiceCollection AddStore(this IServiceCollection services) => services;

    public static IServiceCollection AddAgents(this IServiceCollection services) => services;

    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        foreach (var type in typeof(ICliCommand).Assembly.GetExportedTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ICliCommand).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(ICliCommand), type);
        }

        return services;
    }
}
