using System.Reflection;
using JobRadar.Cli.Commands;
using JobRadar.Core.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Cli.Wiring;

/// <summary>
/// Wiring lives in Cli because Config, Sources, Store and Agents may not take a DI package (SPEC §3.2 dependency budget).
/// Registration is by convention so implementers never edit wiring (TASKS "Rules").
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfig(this IServiceCollection services) => services;

    /// <summary>Registers every public non-abstract <see cref="IJobSource"/> of the Sources assembly (none exist until T-01.12).</summary>
    public static IServiceCollection AddSources(this IServiceCollection services)
    {
        foreach (var type in typeof(JobRadar.Sources.SourcesMarker).Assembly.GetExportedTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IJobSource).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(IJobSource), type);
        }

        return services;
    }

    // The repository scan of the Store assembly is added by the director with the first repository (T-01.08); there is no repository type to scan for yet.
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
