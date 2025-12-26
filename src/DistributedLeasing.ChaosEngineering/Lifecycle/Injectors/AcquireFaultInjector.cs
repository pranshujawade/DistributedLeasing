using DistributedLeasing.ChaosEngineering.Policies.Abstractions;
using DistributedLeasing.ChaosEngineering.Observability;

namespace DistributedLeasing.ChaosEngineering.Lifecycle.Injectors
{
    /// <summary>
    /// Fault injector for lease acquisition operations.
    /// </summary>
    public class AcquireFaultInjector : FaultInjectorBase
    {
        /// <summary>
        /// Initializes a new instance of the AcquireFaultInjector class.
        /// </summary>
        /// <param name="policy">The fault decision policy for acquire operations.</param>
        /// <param name="observer">The chaos observer for event notification (optional).</param>
        public AcquireFaultInjector(
            IFaultDecisionPolicy? policy = null,
            IChaosObserver? observer = null)
            : base("AcquireAsync", policy, observer)
        {
        }
    }
}
