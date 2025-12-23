#if NET5_0_OR_GREATER
using System.Diagnostics;

namespace DistributedLeasing.Core.Observability;

/// <summary>
/// Provides OpenTelemetry-compatible distributed tracing for lease operations.
/// </summary>
/// <remarks>
/// <para>
/// This class exposes activities (spans) following OpenTelemetry semantic conventions.
/// Traces can be consumed by Jaeger, Zipkin, Azure Monitor, or any OTEL-compatible backend.
/// </para>
/// <para>
/// <strong>Integration Example (ASP.NET Core):</strong>
/// </para>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(tracing => tracing
///         .AddSource("DistributedLeasing")
///         .AddAzureMonitorTraceExporter());
/// </code>
/// </remarks>
public static class LeasingActivitySource
{
    /// <summary>
    /// ActivitySource for distributed leasing operations.
    /// </summary>
    public static readonly ActivitySource Source = new("DistributedLeasing", "1.0.1");

    /// <summary>
    /// Operation names for standardized activity naming.
    /// </summary>
    public static class Operations
    {
        /// <summary>Lease acquisition operation.</summary>
        public const string Acquire = "Lease.Acquire";
        
        /// <summary>Lease try-acquire operation (non-blocking).</summary>
        public const string TryAcquire = "Lease.TryAcquire";
        
        /// <summary>Lease renewal operation.</summary>
        public const string Renew = "Lease.Renew";
        
        /// <summary>Lease release operation.</summary>
        public const string Release = "Lease.Release";
        
        /// <summary>Lease break operation (administrative).</summary>
        public const string Break = "Lease.Break";
        
        /// <summary>Auto-renewal background task.</summary>
        public const string AutoRenewal = "Lease.AutoRenewal";
    }

    /// <summary>
    /// Tag keys for activity attributes following OpenTelemetry semantic conventions.
    /// </summary>
    public static class Tags
    {
        /// <summary>Name of the lease being operated on.</summary>
        public const string LeaseName = "lease.name";
        
        /// <summary>Unique identifier of the lease instance.</summary>
        public const string LeaseId = "lease.id";
        
        /// <summary>Provider type (e.g., "BlobLeaseProvider", "RedisLeaseProvider").</summary>
        public const string Provider = "lease.provider";
        
        /// <summary>Lease duration in seconds.</summary>
        public const string Duration = "lease.duration_seconds";
        
        /// <summary>Acquisition timeout in seconds.</summary>
        public const string Timeout = "lease.timeout_seconds";
        
        /// <summary>Result of the operation (success/failure/timeout).</summary>
        public const string Result = "lease.result";
        
        /// <summary>Auto-renewal enabled flag.</summary>
        public const string AutoRenew = "lease.auto_renew";
        
        /// <summary>Number of renewal retries attempted.</summary>
        public const string RetryAttempts = "lease.retry_attempts";
        
        /// <summary>Reason for lease loss.</summary>
        public const string LossReason = "lease.loss_reason";
        
        /// <summary>Exception type when operation fails.</summary>
        public const string ExceptionType = "exception.type";
        
        /// <summary>Exception message when operation fails.</summary>
        public const string ExceptionMessage = "exception.message";
    }

    /// <summary>
    /// Result values for the Result tag.
    /// </summary>
    public static class Results
    {
        /// <summary>Operation completed successfully.</summary>
        public const string Success = "success";
        
        /// <summary>Operation failed due to an error.</summary>
        public const string Failure = "failure";
        
        /// <summary>Operation timed out.</summary>
        public const string Timeout = "timeout";
        
        /// <summary>Lease was already held (for TryAcquire).</summary>
        public const string AlreadyHeld = "already_held";
        
        /// <summary>Lease was lost during operation.</summary>
        public const string Lost = "lost";
    }
}
#endif
