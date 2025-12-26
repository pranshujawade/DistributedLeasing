using DistributedLeasing.ChaosEngineering.Policies.Abstractions;
using DistributedLeasing.ChaosEngineering.Observability;

namespace DistributedLeasing.ChaosEngineering.Lifecycle.Injectors
{
    /// <summary>
    /// Fault injector for lease renewal operations.
    /// Enables testing of auto-renewal failure scenarios.
    /// </summary>
    public class RenewFaultInjector : FaultInjectorBase
    {
        /// <summary>
        /// Initializes a new instance of the RenewFaultInjector class.
        /// </summary>
        /// <param name="policy">The fault decision policy for renew operations.</param>
        /// <param name="observer">The chaos observer for event notification (optional).</param>
        public RenewFaultInjector(
            IFaultDecisionPolicy? policy = null,
            IChaosObserver? observer = null)
            : base("RenewAsync", policy, observer)
        {
        }
    }
}
