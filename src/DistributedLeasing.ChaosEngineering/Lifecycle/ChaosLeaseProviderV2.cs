using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.ChaosEngineering.Configuration;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Lifecycle.Injectors;
using DistributedLeasing.ChaosEngineering.Observability;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Lifecycle
{
    /// <summary>
    /// Advanced chaos engineering wrapper for ILeaseProvider using the new SOLID architecture.
    /// Supports configuration-driven fault injection with full lifecycle coverage.
    /// </summary>
    /// <remarks>
    /// This is the next-generation chaos provider that uses the abstraction layer introduced in Phase 1.
    /// It provides more flexibility, better observability, and cleaner separation of concerns compared
    /// to the legacy ChaosLeaseProvider.
    /// </remarks>
    public class ChaosLeaseProviderV2 : ILeaseProvider
    {
        private readonly ILeaseProvider _innerProvider;
        private readonly ChaosOptions _options;
        private readonly AcquireFaultInjector _acquireInjector;
        private readonly BreakFaultInjector _breakInjector;
        private readonly IChaosObserver? _observer;

        /// <summary>
        /// Initializes a new instance of the ChaosLeaseProviderV2 class.
        /// </summary>
        /// <param name="innerProvider">The actual lease provider to wrap.</param>
        /// <param name="options">Chaos configuration options.</param>
        /// <param name="observer">Observer for chaos events (optional).</param>
        public ChaosLeaseProviderV2(
            ILeaseProvider innerProvider,
            ChaosOptions options,
            IChaosObserver? observer = null)
        {
            _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _observer = observer;

            // Validate configuration
            if (_options.FailFastOnConfigurationErrors)
            {
                var validator = new ChaosOptionsValidator();
                validator.Validate(_options).ThrowIfInvalid();
            }

            // Create fault injectors for provider-level operations
            _acquireInjector = new AcquireFaultInjector(
                GetPolicyForOperation("AcquireAsync"),
                _observer);

            _breakInjector = new BreakFaultInjector(
                GetPolicyForOperation("BreakAsync"),
                _observer);
        }

        /// <inheritdoc/>
        public async Task<ILease?> AcquireLeaseAsync(
            string leaseName,
            TimeSpan duration,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return await _innerProvider.AcquireLeaseAsync(leaseName, duration, cancellationToken)
                    .ConfigureAwait(false);
            }

            var context = CreateFaultContext("AcquireAsync", leaseName);

            // Inject fault if configured
            if (_acquireInjector.ShouldInjectFault(context))
            {
                await _acquireInjector.InjectFaultAsync(context, cancellationToken).ConfigureAwait(false);
            }

            // Acquire the actual lease
            var lease = await _innerProvider.AcquireLeaseAsync(leaseName, duration, cancellationToken)
                .ConfigureAwait(false);

            // Wrap lease with chaos if acquired
            if (lease != null)
            {
                var renewPolicy = GetPolicyForOperation("RenewAsync");
                var releasePolicy = GetPolicyForOperation("ReleaseAsync");
                
                return new ChaosLease(
                    lease,
                    renewPolicy,
                    releasePolicy,
                    _observer,
                    _options.ProviderName);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task BreakLeaseAsync(string leaseName, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                await _innerProvider.BreakLeaseAsync(leaseName, cancellationToken).ConfigureAwait(false);
                return;
            }

            var context = CreateFaultContext("BreakAsync", leaseName);

            // Inject fault if configured
            if (_breakInjector.ShouldInjectFault(context))
            {
                await _breakInjector.InjectFaultAsync(context, cancellationToken).ConfigureAwait(false);
            }

            await _innerProvider.BreakLeaseAsync(leaseName, cancellationToken).ConfigureAwait(false);
        }

        private FaultContext CreateFaultContext(string operation, string leaseName)
        {
            var context = new FaultContext
            {
                Operation = operation,
                LeaseName = leaseName,
                Timestamp = DateTimeOffset.UtcNow,
                ProviderName = _options.ProviderName ?? "ChaosLeaseProviderV2"
            };

            // Add global metadata
            foreach (var kvp in _options.GlobalMetadata)
            {
                context.Metadata[kvp.Key] = kvp.Value;
            }

            // Add environment tags
            foreach (var kvp in _options.EnvironmentTags)
            {
                context.Metadata[$"Env_{kvp.Key}"] = kvp.Value;
            }

            return context;
        }

        private IFaultDecisionPolicy? GetPolicyForOperation(string operationName)
        {
            // Check for operation-specific configuration
            if (_options.OperationOptions.TryGetValue(operationName, out var operationOptions))
            {
                // Operation-level policy takes precedence
                if (operationOptions.Policy != null)
                {
                    return operationOptions.Policy;
                }

                // Check if operation is explicitly disabled
                if (operationOptions.Enabled.HasValue && !operationOptions.Enabled.Value)
                {
                    return null;
                }
            }

            // Fall back to global default policy
            return _options.DefaultPolicy;
        }
    }
}
