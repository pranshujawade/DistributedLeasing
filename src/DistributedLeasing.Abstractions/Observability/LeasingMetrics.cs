#if NET8_0_OR_GREATER
using System.Diagnostics.Metrics;

namespace DistributedLeasing.Abstractions.Observability;

/// <summary>
/// Provides OpenTelemetry-compatible metrics for distributed leasing operations.
/// </summary>
/// <remarks>
/// <para>
/// This class exposes metrics following OpenTelemetry semantic conventions for observability.
/// Metrics can be consumed by Prometheus, Azure Monitor, DataDog, or any OTEL-compatible backend.
/// </para>
/// <para>
/// <strong>Integration Example (ASP.NET Core):</strong>
/// </para>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(metrics => metrics
///         .AddMeter("DistributedLeasing")
///         .AddPrometheusExporter());
/// </code>
/// </remarks>
public static class LeasingMetrics
{
    private static readonly Meter Meter = new("DistributedLeasing", "1.0.1");

    /// <summary>
    /// Total number of lease acquisition attempts.
    /// </summary>
    /// <remarks>
    /// Tags: provider, lease_name, result (success/failure/timeout)
    /// </remarks>
    public static readonly Counter<long> LeaseAcquisitions = Meter.CreateCounter<long>(
        name: "leasing.acquisitions.total",
        unit: "{acquisitions}",
        description: "Total number of lease acquisition attempts");

    /// <summary>
    /// Duration of lease acquisition operations in milliseconds.
    /// </summary>
    /// <remarks>
    /// Tags: provider, result (success/failure/timeout)
    /// Use for percentiles (P50, P95, P99) to track acquisition latency.
    /// </remarks>
    public static readonly Histogram<double> LeaseAcquisitionDuration = Meter.CreateHistogram<double>(
        name: "leasing.acquisition.duration",
        unit: "ms",
        description: "Duration of lease acquisition attempts in milliseconds");

    /// <summary>
    /// Total number of lease renewal attempts.
    /// </summary>
    /// <remarks>
    /// Tags: provider, lease_name, result (success/failure)
    /// </remarks>
    public static readonly Counter<long> LeaseRenewals = Meter.CreateCounter<long>(
        name: "leasing.renewals.total",
        unit: "{renewals}",
        description: "Total number of lease renewal attempts");

    /// <summary>
    /// Duration of lease renewal operations in milliseconds.
    /// </summary>
    /// <remarks>
    /// Tags: provider, result (success/failure)
    /// </remarks>
    public static readonly Histogram<double> LeaseRenewalDuration = Meter.CreateHistogram<double>(
        name: "leasing.renewal.duration",
        unit: "ms",
        description: "Duration of lease renewal operations in milliseconds");

    /// <summary>
    /// Total number of lease renewal failures.
    /// </summary>
    /// <remarks>
    /// Tags: provider, lease_name, reason (timeout/conflict/lost)
    /// Alert on high rates to detect provider issues.
    /// </remarks>
    public static readonly Counter<long> LeaseRenewalFailures = Meter.CreateCounter<long>(
        name: "leasing.renewal.failures.total",
        unit: "{failures}",
        description: "Total number of failed lease renewals");

    /// <summary>
    /// Total number of leases definitively lost (cannot be recovered).
    /// </summary>
    /// <remarks>
    /// Tags: provider, lease_name, reason
    /// CRITICAL: Alert on any occurrence - indicates leader election failover or data loss.
    /// </remarks>
    public static readonly Counter<long> LeasesLost = Meter.CreateCounter<long>(
        name: "leasing.leases_lost.total",
        unit: "{leases}",
        description: "Total number of leases definitively lost");

    /// <summary>
    /// Number of currently active leases held by this instance.
    /// </summary>
    /// <remarks>
    /// Tags: provider
    /// ObservableGauge updates asynchronously - current snapshot of active leases.
    /// </remarks>
    public static readonly ObservableGauge<int> ActiveLeases = Meter.CreateObservableGauge<int>(
        name: "leasing.active_leases.current",
        observeValue: () => ActiveLeaseTracker.GetActiveCount(),
        unit: "{leases}",
        description: "Number of currently active leases held by this instance");

    /// <summary>
    /// Time since last successful renewal for a lease in seconds.
    /// </summary>
    /// <remarks>
    /// Tags: provider, lease_name
    /// Alert if exceeds renewal_interval + safety_threshold to detect stuck renewals.
    /// </remarks>
    public static readonly Histogram<double> TimeSinceLastRenewal = Meter.CreateHistogram<double>(
        name: "leasing.time_since_last_renewal",
        unit: "s",
        description: "Time elapsed since last successful renewal in seconds");

    /// <summary>
    /// Number of retry attempts during renewal before success or final failure.
    /// </summary>
    /// <remarks>
    /// Tags: provider, result (success/failure)
    /// High retry counts indicate provider instability.
    /// </remarks>
    public static readonly Histogram<int> RenewalRetryAttempts = Meter.CreateHistogram<int>(
        name: "leasing.renewal.retry_attempts",
        unit: "{attempts}",
        description: "Number of retry attempts during lease renewal");
}

/// <summary>
/// Internal tracker for active lease count (used by ObservableGauge).
/// </summary>
internal static class ActiveLeaseTracker
{
    private static int _activeCount;

    public static void Increment() => Interlocked.Increment(ref _activeCount);
    public static void Decrement() => Interlocked.Decrement(ref _activeCount);
    public static int GetActiveCount() => Volatile.Read(ref _activeCount);
}
#endif
