using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;
using DistributedLeasing.ChaosEngineering.Observability;

namespace DistributedLeasing.ChaosEngineering.Lifecycle.Injectors
{
    /// <summary>
    /// Abstract base class for fault injectors that provides common functionality.
    /// Implements the Template Method pattern for fault injection flow.
    /// </summary>
    public abstract class FaultInjectorBase : IFaultInjector
    {
        private readonly IFaultDecisionPolicy? _policy;
        private readonly IChaosObserver? _observer;

        /// <summary>
        /// Initializes a new instance of the FaultInjectorBase class.
        /// </summary>
        /// <param name="operationName">The name of the operation this injector handles.</param>
        /// <param name="policy">The fault decision policy (optional).</param>
        /// <param name="observer">The chaos observer for event notification (optional).</param>
        protected FaultInjectorBase(
            string operationName,
            IFaultDecisionPolicy? policy = null,
            IChaosObserver? observer = null)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operationName));
            }

            OperationName = operationName;
            _policy = policy;
            _observer = observer;
        }

        /// <inheritdoc/>
        public string OperationName { get; }

        /// <summary>
        /// Gets the fault decision policy for this injector.
        /// </summary>
        protected IFaultDecisionPolicy? Policy => _policy;

        /// <summary>
        /// Gets the chaos observer for this injector.
        /// </summary>
        protected IChaosObserver? Observer => _observer;

        /// <inheritdoc/>
        public virtual bool ShouldInjectFault(FaultContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_policy == null)
            {
                return false;
            }

            var decision = _policy.Evaluate(context);
            return decision.ShouldInjectFault && decision.FaultStrategy != null;
        }

        /// <inheritdoc/>
        public virtual async Task InjectFaultAsync(FaultContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_policy == null)
            {
                _observer?.OnFaultSkipped(context, "No policy configured");
                return;
            }

            var decision = _policy.Evaluate(context);
            _observer?.OnFaultDecisionMade(context, decision, _policy.Name);

            if (!decision.ShouldInjectFault || decision.FaultStrategy == null)
            {
                _observer?.OnFaultSkipped(context, decision.Reason ?? "Policy decided to skip fault injection");
                return;
            }

            var strategy = decision.FaultStrategy;

            if (!strategy.CanExecute(context))
            {
                _observer?.OnFaultSkipped(context, $"Strategy '{strategy.Name}' cannot execute for this context");
                return;
            }

            try
            {
                _observer?.OnFaultExecuting(context, strategy);
                var startTime = DateTimeOffset.UtcNow;

                await ExecuteFaultAsync(context, strategy, cancellationToken).ConfigureAwait(false);

                var duration = DateTimeOffset.UtcNow - startTime;
                _observer?.OnFaultExecuted(context, strategy, duration);
            }
            catch (Exception ex)
            {
                _observer?.OnFaultExecutionFailed(context, strategy, ex);
                throw;
            }
        }

        /// <summary>
        /// Executes the fault strategy. Override this to customize fault execution behavior.
        /// </summary>
        /// <param name="context">The fault context.</param>
        /// <param name="strategy">The fault strategy to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous fault execution.</returns>
        protected virtual Task ExecuteFaultAsync(
            FaultContext context,
            IFaultStrategy strategy,
            CancellationToken cancellationToken)
        {
            return strategy.ExecuteAsync(context, cancellationToken);
        }
    }
}
