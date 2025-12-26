using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Policies.Abstractions
{
    /// <summary>
    /// Represents the decision result from a fault decision policy.
    /// Indicates whether a fault should be injected and which strategy to use.
    /// </summary>
    public class FaultDecision
    {
        /// <summary>
        /// Gets or sets a value indicating whether a fault should be injected.
        /// </summary>
        public bool ShouldInjectFault { get; set; }

        /// <summary>
        /// Gets or sets the fault strategy to use, if a fault should be injected.
        /// </summary>
        public IFaultStrategy? FaultStrategy { get; set; }

        /// <summary>
        /// Gets or sets the reason for the decision (for logging and observability).
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the decision.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Creates a decision to inject a fault with the specified strategy.
        /// </summary>
        /// <param name="strategy">The fault strategy to execute.</param>
        /// <param name="reason">The reason for injecting this fault.</param>
        /// <returns>A <see cref="FaultDecision"/> indicating fault injection.</returns>
        public static FaultDecision Inject(IFaultStrategy strategy, string? reason = null)
        {
            return new FaultDecision
            {
                ShouldInjectFault = true,
                FaultStrategy = strategy,
                Reason = reason ?? $"Policy decided to inject {strategy.Name} fault"
            };
        }

        /// <summary>
        /// Creates a decision to skip fault injection.
        /// </summary>
        /// <param name="reason">The reason for not injecting a fault.</param>
        /// <returns>A <see cref="FaultDecision"/> indicating no fault injection.</returns>
        public static FaultDecision Skip(string? reason = null)
        {
            return new FaultDecision
            {
                ShouldInjectFault = false,
                Reason = reason ?? "Policy decided to skip fault injection"
            };
        }
    }
}
