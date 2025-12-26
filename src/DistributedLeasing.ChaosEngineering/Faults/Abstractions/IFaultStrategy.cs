using System.Threading;
using System.Threading.Tasks;

namespace DistributedLeasing.ChaosEngineering.Faults.Abstractions
{
    /// <summary>
    /// Defines the contract for a fault injection strategy.
    /// Implementations provide specific fault behaviors (delay, exception, timeout, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface follows the Strategy pattern, allowing different fault types
    /// to be plugged in without modifying the chaos provider infrastructure.
    /// </para>
    /// <para>
    /// Implementations must be thread-safe as they may be invoked concurrently.
    /// </para>
    /// </remarks>
    public interface IFaultStrategy
    {
        /// <summary>
        /// Gets the unique name of this fault strategy (e.g., "Delay", "Exception", "Timeout").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a human-readable description of what this fault strategy does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the severity level of this fault type.
        /// </summary>
        FaultSeverity Severity { get; }

        /// <summary>
        /// Determines whether this fault strategy can be executed in the given context.
        /// </summary>
        /// <param name="context">The execution context for the current operation.</param>
        /// <returns>
        /// <c>true</c> if the fault can be executed in this context; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Use this method to implement conditional fault injection based on context properties
        /// such as operation type, lease name, attempt number, etc.
        /// </remarks>
        bool CanExecute(FaultContext context);

        /// <summary>
        /// Executes the fault injection asynchronously.
        /// </summary>
        /// <param name="context">The execution context for the current operation.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the fault execution.</param>
        /// <returns>A task that represents the asynchronous fault injection operation.</returns>
        /// <remarks>
        /// <para>
        /// Implementations should respect the <paramref name="cancellationToken"/> and
        /// complete promptly when cancellation is requested.
        /// </para>
        /// <para>
        /// This method may throw exceptions as part of the fault injection logic.
        /// The chaos provider will NOT catch or suppress these exceptions unless configured to do so.
        /// </para>
        /// </remarks>
        Task ExecuteAsync(FaultContext context, CancellationToken cancellationToken = default);
    }
}
