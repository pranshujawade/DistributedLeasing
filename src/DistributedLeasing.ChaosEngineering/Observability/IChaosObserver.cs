using System;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Observability
{
    /// <summary>
    /// Defines the contract for observing chaos engineering events.
    /// Implementations can log, emit metrics, create traces, or perform other observability tasks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface follows the Observer pattern, allowing multiple observers
    /// to be notified of chaos events without coupling the chaos provider to specific implementations.
    /// </para>
    /// <para>
    /// Implementations should be lightweight and non-blocking to avoid impacting test performance.
    /// </para>
    /// </remarks>
    public interface IChaosObserver
    {
        /// <summary>
        /// Called when a fault decision is made by a policy.
        /// </summary>
        /// <param name="context">The fault context for the operation.</param>
        /// <param name="decision">The decision made by the policy.</param>
        /// <param name="policyName">The name of the policy that made the decision.</param>
        void OnFaultDecisionMade(FaultContext context, FaultDecision decision, string policyName);

        /// <summary>
        /// Called before a fault is executed.
        /// </summary>
        /// <param name="context">The fault context for the operation.</param>
        /// <param name="strategy">The fault strategy being executed.</param>
        void OnFaultExecuting(FaultContext context, IFaultStrategy strategy);

        /// <summary>
        /// Called after a fault has been successfully executed.
        /// </summary>
        /// <param name="context">The fault context for the operation.</param>
        /// <param name="strategy">The fault strategy that was executed.</param>
        /// <param name="duration">The time taken to execute the fault.</param>
        void OnFaultExecuted(FaultContext context, IFaultStrategy strategy, TimeSpan duration);

        /// <summary>
        /// Called when fault execution fails or throws an exception.
        /// </summary>
        /// <param name="context">The fault context for the operation.</param>
        /// <param name="strategy">The fault strategy that failed.</param>
        /// <param name="exception">The exception that occurred.</param>
        void OnFaultExecutionFailed(FaultContext context, IFaultStrategy strategy, Exception exception);

        /// <summary>
        /// Called when fault injection is skipped (decision was to not inject).
        /// </summary>
        /// <param name="context">The fault context for the operation.</param>
        /// <param name="reason">The reason fault injection was skipped.</param>
        void OnFaultSkipped(FaultContext context, string reason);
    }
}
