using System.Reflection;
using Infrastructure.RabbitMQ;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Clients.Api.Diagnostics;

public static class OpenTelemetryConfigurationExtensions
{
    public static WebApplicationBuilder AddOpenTelemetry(this WebApplicationBuilder builder)
    {
        const string serviceName = "Clients.Api";

        var otlpEndpoint = new Uri(builder.Configuration.GetValue<string>("OTLP_Endpoint")!);

        // builder.Logging
        //     .Configure(options =>
        //     {
        //         options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId
        //                                           | ActivityTrackingOptions.TraceId
        //                                           | ActivityTrackingOptions.ParentId
        //                                           | ActivityTrackingOptions.Baggage
        //                                           | ActivityTrackingOptions.Tags;
        //     })
        //     .AddOpenTelemetry(
        //     options =>
        //     {
        //         options.IncludeScopes = true;
        //         options.IncludeFormattedMessage = true;
        //         options.ParseStateValues = true;
        //
        //         options
        //             .SetResourceBuilder(ResourceBuilder.CreateDefault()
        //                 .AddService(serviceName));
        //
        //         options.AddConsoleExporter();
        //     }
        // );

        // builder.Services
        //     .ConfigureOpenTelemetryTracerProvider(provider =>
        //         provider.SetSampler(new RateSampler(0.25)));
        //
        // builder.Services
        //     .ConfigureOpenTelemetryTracerProvider(provider =>
        //         provider.SetSampler<OpenTelemetry.Trace.AlwaysOffSampler>());

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(serviceName,
                        "Dometrain.Courses.OpenTelemetry",
                        Assembly.GetExecutingAssembly().GetName().Version!.ToString())
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("team", "Dometrain")
                    });
            })
            .WithTracing(tracing =>
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation(
                        options => options.RecordException = true)
                    .AddNpgsql()
                    .AddSource(RabbitMqDiagnostics.ActivitySourceName)
                    .AddRedisInstrumentation()
                    .SetSampler<AlwaysOnSampler>()
                    // .AddConsoleExporter()
                    .AddOtlpExporter(options =>
                    {
                        options.Protocol = OtlpExportProtocol.Grpc;
                        options.Endpoint = otlpEndpoint;
                    })
            )
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Metrics provides by ASP.NET
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter(ApplicationDiagnostics.Meter.Name)
                    // .AddConsoleExporter()
                    .AddOtlpExporter(options =>
                        options.Endpoint = otlpEndpoint)
            )
            .WithLogging(
                logging =>
                    logging
                        // .AddConsoleExporter()
                        .AddOtlpExporter(options =>
                            options.Endpoint = otlpEndpoint)
            );

        return builder;
    }
}