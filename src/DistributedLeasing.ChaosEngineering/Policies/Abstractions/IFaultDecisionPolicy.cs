using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Policies.Abstractions
{
    /// <summary>
    /// Defines the contract for a fault decision policy.
    /// Implementations determine whether and which faults should be injected based on context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface follows the Strategy pattern, allowing different decision-making
    /// strategies to be used (probabilistic, deterministic, threshold-based, conditional, etc.).
    /// </para>
    /// <para>
    /// Implementations must be thread-safe as they may be invoked concurrently.
    /// </para>
    /// </remarks>
    public interface IFaultDecisionPolicy
    {
        /// <summary>
        /// Gets the name of this policy (for logging and debugging).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Evaluates whether a fault should be injected for the given context.
        /// </summary>
        /// <param name="context">The execution context for the current operation.</param>
        /// <returns>
        /// A <see cref="FaultDecision"/> indicating whether to inject a fault and which strategy to use.
        /// </returns>
        /// <remarks>
        /// This method should be deterministic for a given context when the policy is configured
        /// for deterministic behavior, or probabilistic when random fault injection is desired.
        /// </remarks>
        FaultDecision Evaluate(FaultContext context);
    }
}
