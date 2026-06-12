using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ControleFinanceiro.Api.Configuration;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";
    
    public string ServiceName { get; set; } = "ControleFinanceiro.Api";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";
    public bool UseConsoleExporter { get; set; } = true;
}

public static class ObservabilityExtensions
{
    private static readonly ActivitySource ActivitySource = new(
        Assembly.GetExecutingAssembly().GetName().Name ?? "ControleFinanceiro.Api",
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0");

    public static ActivitySource GetActivitySource() => ActivitySource;

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService(
                serviceName: options.ServiceName, 
                serviceVersion: options.ServiceVersion))
            .WithTracing(tracing =>
            {
                tracing.AddSource(ActivitySource.Name);
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetTag("http.request.body_size", request.ContentLength);
                        activity.SetTag("http.request.method", request.Method);
                        activity.SetTag("url.path", request.Path);
                        activity.SetTag("url.query", request.QueryString.ToString());
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequestMessage = (activity, request) =>
                    {
                        activity.SetTag("http.request.method", request.Method?.Method);
                        activity.SetTag("url.full", request.RequestUri?.ToString());
                    };
                });

                if (options.UseConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();

                metrics.AddMeter("Microsoft.AspNetCore.Hosting");
                metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");

                if (options.UseConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }
}