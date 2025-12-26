using DistributedLeasing.ChaosEngineering.Policies.Abstractions;
using DistributedLeasing.ChaosEngineering.Observability;

namespace DistributedLeasing.ChaosEngineering.Lifecycle.Injectors
{
    /// <summary>
    /// Fault injector for lease release operations.
    /// Enables testing of scenarios where explicit lease release fails.
    /// </summary>
    public class ReleaseFaultInjector : FaultInjectorBase
    {
        /// <summary>
        /// Initializes a new instance of the ReleaseFaultInjector class.
        /// </summary>
        /// <param name="policy">The fault decision policy for release operations.</param>
        /// <param name="observer">The chaos observer for event notification (optional).</param>
        public ReleaseFaultInjector(
            IFaultDecisionPolicy? policy = null,
            IChaosObserver? observer = null)
            : base("ReleaseAsync", policy, observer)
        {
        }
    }
}
