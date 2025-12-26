using System;
using System.Collections.Generic;

namespace DistributedLeasing.ChaosEngineering.Faults.Abstractions
{
    /// <summary>
    /// Represents the execution context for a fault injection operation.
    /// Provides metadata and state information for fault decision-making and execution.
    /// </summary>
    public class FaultContext
    {
        /// <summary>
        /// Gets or sets the unique identifier for this fault context.
        /// </summary>
        public string ContextId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the operation being executed (e.g., "Acquire", "Renew", "Release", "Break").
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the lease being operated on.
        /// </summary>
        public string? LeaseName { get; set; }

        /// <summary>
        /// Gets or sets the lease identifier, if available.
        /// </summary>
        public string? LeaseId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this context was created.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the attempt number for retry scenarios (1-based).
        /// </summary>
        public int AttemptNumber { get; set; } = 1;

        /// <summary>
        /// Gets or sets additional metadata for this operation.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets whether this operation is part of an automatic retry.
        /// </summary>
        public bool IsRetry { get; set; }

        /// <summary>
        /// Gets or sets the duration parameter for the operation, if applicable.
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets the provider name (e.g., "Blob", "Cosmos", "Redis").
        /// </summary>
        public string? ProviderName { get; set; }
    }
}
