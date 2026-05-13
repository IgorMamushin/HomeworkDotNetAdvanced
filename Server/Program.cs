using HomeworkAdv;
using Microsoft.Extensions.Logging;
using Models.Observability;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        const string ServiceName = "CacheService";

        var resourceBuilder = ResourceBuilder.CreateDefault()
                                             .AddService(serviceName: ServiceName);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);

            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                options.IncludeScopes = true;
            });

            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);

                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;

                options.AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = new Uri(
                        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                        ?? "http://localhost:4317");
                });
            });
        });

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                                      .SetResourceBuilder(resourceBuilder)
                                      .AddSource(ServiceTrace.ActivitySource.Name)
                                      .AddOtlpExporter(options =>
                                      {
                                          options.Endpoint = new Uri(
                                              Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                              ?? "http://localhost:4317");
                                      })
                                      .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
                                     .SetResourceBuilder(resourceBuilder)
                                     .AddMeter(ServiceMetrics.MeterName)
                                     .AddRuntimeInstrumentation()
                                     .AddView("cache_request_duration_ms", new ExplicitBucketHistogramConfiguration
                                     {
                                         Boundaries =
                                         [
                                             0.1,
                                             0.5,
                                             1,
                                             2,
                                             5,
                                             10,
                                             25,
                                             50,
                                             100,
                                             250,
                                             500,
                                             1000,
                                             2500,
                                             5000
                                         ]
                                     })
                                     .AddOtlpExporter(options =>
                                     {
                                         options.Endpoint = new Uri(
                                             Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                             ?? "http://localhost:4317");
                                     })
                                     .Build();

        var globalCts = new CancellationTokenSource();
        Console.WriteLine("Press ctrl+c to cancel command");
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            globalCts.Cancel();
            Console.WriteLine("Cancelling...");
        };

        using var store = new SimpleStore();

        var logger = loggerFactory.CreateLogger<TcpServer>();

        var server = new TcpServer(store, logger);
        await server.StartAsync(globalCts.Token);
    }
}