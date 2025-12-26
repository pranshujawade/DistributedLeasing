using System;
using System.Collections.Generic;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Configuration
{
    /// <summary>
    /// Configuration options for chaos engineering fault injection.
    /// Supports the Options pattern for integration with ASP.NET Core configuration.
    /// </summary>
    public class ChaosOptions
    {
        /// <summary>
        /// Gets or sets whether chaos engineering is enabled globally.
        /// Default is false for safety.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the global seed for random number generation.
        /// Setting this enables reproducible chaos scenarios.
        /// If null, uses a time-based seed.
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Gets or sets the default fault decision policy to use when no operation-specific policy is defined.
        /// </summary>
        public IFaultDecisionPolicy? DefaultPolicy { get; set; }

        /// <summary>
        /// Gets or sets the collection of fault strategies available for injection.
        /// </summary>
        public ICollection<IFaultStrategy> FaultStrategies { get; set; } = new List<IFaultStrategy>();

        /// <summary>
        /// Gets or sets operation-specific chaos configurations.
        /// Key is the operation name (e.g., "AcquireAsync", "RenewAsync").
        /// </summary>
        public Dictionary<string, OperationChaosOptions> OperationOptions { get; set; } 
            = new Dictionary<string, OperationChaosOptions>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the global maximum allowed fault injection rate (faults per second).
        /// This acts as a safety limit to prevent overwhelming the system.
        /// Default is null (no limit).
        /// </summary>
        public double? MaxFaultRate { get; set; }

        /// <summary>
        /// Gets or sets the time window for rate limiting in seconds.
        /// Default is 60 seconds.
        /// </summary>
        public int RateLimitWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets whether to enable detailed observability (metrics, tracing, logging).
        /// Default is true.
        /// </summary>
        public bool EnableObservability { get; set; } = true;

        /// <summary>
        /// Gets or sets the provider name for telemetry tagging.
        /// </summary>
        public string? ProviderName { get; set; }

        /// <summary>
        /// Gets or sets custom metadata to attach to all fault contexts.
        /// </summary>
        public Dictionary<string, object> GlobalMetadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets whether to fail fast on configuration errors.
        /// If true, invalid configuration throws during initialization.
        /// If false, logs warnings and disables chaos.
        /// Default is true.
        /// </summary>
        public bool FailFastOnConfigurationErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum severity level for fault injection.
        /// Faults below this severity will not be injected.
        /// Default is Low (all faults allowed).
        /// </summary>
        public FaultSeverity MinimumSeverity { get; set; } = FaultSeverity.Low;

        /// <summary>
        /// Gets or sets environment-specific tags for conditional chaos.
        /// Example: { "Environment": "Staging", "Region": "US-West" }
        /// </summary>
        public Dictionary<string, string> EnvironmentTags { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Configuration options for chaos engineering on a specific operation.
    /// </summary>
    public class OperationChaosOptions
    {
        /// <summary>
        /// Gets or sets whether chaos is enabled for this specific operation.
        /// Overrides the global Enabled setting.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Gets or sets the fault decision policy for this operation.
        /// Overrides the global DefaultPolicy.
        /// </summary>
        public IFaultDecisionPolicy? Policy { get; set; }

        /// <summary>
        /// Gets or sets the collection of fault strategies available for this operation.
        /// If empty, uses the global FaultStrategies.
        /// </summary>
        public ICollection<IFaultStrategy> FaultStrategies { get; set; } = new List<IFaultStrategy>();

        /// <summary>
        /// Gets or sets operation-specific metadata.
        /// Merged with global metadata in fault contexts.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the maximum fault injection rate for this operation (faults per second).
        /// Overrides the global MaxFaultRate.
        /// </summary>
        public double? MaxFaultRate { get; set; }

        /// <summary>
        /// Gets or sets the minimum severity level for this operation.
        /// Overrides the global MinimumSeverity.
        /// </summary>
        public FaultSeverity? MinimumSeverity { get; set; }

        /// <summary>
        /// Gets or sets lease name patterns this configuration applies to (regex supported).
        /// If empty, applies to all leases for this operation.
        /// </summary>
        public ICollection<string> LeaseNamePatterns { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets conditional rules for this operation.
        /// Example: only inject faults during retry attempts, or after N successful operations.
        /// </summary>
        public OperationConditions? Conditions { get; set; }
    }

    /// <summary>
    /// Conditional rules for operation-specific fault injection.
    /// </summary>
    public class OperationConditions
    {
        /// <summary>
        /// Gets or sets whether to only inject faults during retry attempts.
        /// </summary>
        public bool OnlyOnRetry { get; set; }

        /// <summary>
        /// Gets or sets the minimum attempt number before injecting faults.
        /// Example: 2 means skip first attempt, start injecting on second attempt.
        /// </summary>
        public int? MinimumAttemptNumber { get; set; }

        /// <summary>
        /// Gets or sets the maximum attempt number for injecting faults.
        /// Example: 5 means stop injecting after the fifth attempt.
        /// </summary>
        public int? MaximumAttemptNumber { get; set; }

        /// <summary>
        /// Gets or sets custom metadata conditions as key-value pairs.
        /// Fault is only injected if context metadata matches all conditions.
        /// </summary>
        public Dictionary<string, object> MetadataConditions { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets time-based conditions for fault injection.
        /// </summary>
        public TimeConditions? TimeConditions { get; set; }
    }

    /// <summary>
    /// Time-based conditions for fault injection.
    /// </summary>
    public class TimeConditions
    {
        /// <summary>
        /// Gets or sets the earliest time of day to inject faults (UTC).
        /// Example: "09:00:00" for 9 AM UTC.
        /// </summary>
        public TimeSpan? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the latest time of day to inject faults (UTC).
        /// Example: "17:00:00" for 5 PM UTC.
        /// </summary>
        public TimeSpan? EndTime { get; set; }

        /// <summary>
        /// Gets or sets days of week when faults can be injected.
        /// Empty means all days are allowed.
        /// </summary>
        public ICollection<DayOfWeek> AllowedDaysOfWeek { get; set; } = new List<DayOfWeek>();

        /// <summary>
        /// Gets or sets the start date for fault injection (inclusive).
        /// </summary>
        public DateTimeOffset? StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date for fault injection (exclusive).
        /// </summary>
        public DateTimeOffset? EndDate { get; set; }
    }
}
