using System.Diagnostics.Metrics;

namespace Models.Observability;

public class ServiceMetrics
{
    public const string MeterName = "CacheService.Metrics";

    public static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        name: "cache_request_duration_ms",
        unit: "ms",
        description: "Socket request processing duration in milliseconds");

    public static readonly Histogram<double> CommandProcessingDuration = Meter.CreateHistogram<double>(
        name: "cache_command_processing_duration_ms",
        unit: "ms",
        description: "Socket command processing duration in milliseconds");

    public static readonly Counter<long> RequestsTotal = Meter.CreateCounter<long>(
        name: "cache_requests_total",
        unit: "{request}",
        description: "Total number of processed socket requests");

    public static readonly Counter<long> RequestErrorsTotal = Meter.CreateCounter<long>(
        name: "cache_request_errors_total",
        unit: "{error}",
        description: "Total number of failed socket requests");

    public static readonly UpDownCounter<long> ActiveConnections = Meter.CreateUpDownCounter<long>(
        name: "cache_active_connections",
        unit: "{connection}",
        description: "Current number of active socket connections");
}