using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Lifecycle
{
    /// <summary>
    /// Interface for fault injection into lease operations.
    /// Enables separation of fault injection logic from lease implementation.
    /// </summary>
    public interface IFaultInjector
    {
        /// <summary>
        /// Gets the name of the operation this injector handles.
        /// </summary>
        string OperationName { get; }

        /// <summary>
        /// Determines whether fault injection should occur for the given context.
        /// </summary>
        /// <param name="context">The fault context containing operation metadata.</param>
        /// <returns>True if a fault should be injected; otherwise, false.</returns>
        bool ShouldInjectFault(FaultContext context);

        /// <summary>
        /// Injects a fault for the given context.
        /// </summary>
        /// <param name="context">The fault context containing operation metadata.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous fault injection operation.</returns>
        Task InjectFaultAsync(FaultContext context, CancellationToken cancellationToken = default);
    }
}
