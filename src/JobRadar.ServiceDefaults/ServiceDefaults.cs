using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JobRadar.ServiceDefaults;

/// <summary>Kernel (P1): logging format and OpenTelemetry. No domain types, at most 400 lines (checked in CI).</summary>
public static class ServiceDefaults
{
    public const string ActivitySourceName = "JobRadar";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o => o.IncludeScopes = false);

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("jobradar"))
            .WithTracing(t => t.AddSource(ActivitySourceName))
            .WithMetrics(m => m.AddRuntimeInstrumentation());

        // Export only when an endpoint is configured (the AppHost sets it); otherwise nothing leaves the process.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }
}
