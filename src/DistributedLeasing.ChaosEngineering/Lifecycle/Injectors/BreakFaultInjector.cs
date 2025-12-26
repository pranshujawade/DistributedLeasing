using DistributedLeasing.ChaosEngineering.Policies.Abstractions;
using DistributedLeasing.ChaosEngineering.Observability;

namespace DistributedLeasing.ChaosEngineering.Lifecycle.Injectors
{
    /// <summary>
    /// Fault injector for lease break operations.
    /// Enables testing of scenarios where forceful lease break fails.
    /// </summary>
    public class BreakFaultInjector : FaultInjectorBase
    {
        /// <summary>
        /// Initializes a new instance of the BreakFaultInjector class.
        /// </summary>
        /// <param name="policy">The fault decision policy for break operations.</param>
        /// <param name="observer">The chaos observer for event notification (optional).</param>
        public BreakFaultInjector(
            IFaultDecisionPolicy? policy = null,
            IChaosObserver? observer = null)
            : base("BreakAsync", policy, observer)
        {
        }
    }
}
