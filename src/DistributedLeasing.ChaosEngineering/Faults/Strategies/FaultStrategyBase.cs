using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Faults.Strategies
{
    /// <summary>
    /// Base class for fault strategy implementations.
    /// Provides common functionality and default behavior.
    /// </summary>
    public abstract class FaultStrategyBase : IFaultStrategy
    {
        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract string Description { get; }

        /// <inheritdoc/>
        public virtual FaultSeverity Severity => FaultSeverity.Medium;

        /// <inheritdoc/>
        public virtual bool CanExecute(FaultContext context)
        {
            // Default: can execute in any context
            // Override in derived classes for conditional execution
            return true;
        }

        /// <inheritdoc/>
        public abstract Task ExecuteAsync(FaultContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that the context is not null.
        /// </summary>
        /// <param name="context">The fault context.</param>
        /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
        protected void ValidateContext(FaultContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), 
                    $"FaultContext cannot be null for {Name} strategy");
            }
        }
    }
}
