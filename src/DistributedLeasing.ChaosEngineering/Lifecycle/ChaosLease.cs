using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Events;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;
using DistributedLeasing.ChaosEngineering.Observability;

namespace DistributedLeasing.ChaosEngineering.Lifecycle
{
    /// <summary>
    /// Chaos engineering wrapper for ILease that injects faults into lease lifecycle operations.
    /// Enables testing of failure scenarios in RenewAsync, ReleaseAsync, and auto-renewal.
    /// </summary>
    public class ChaosLease : ILease
    {
        private readonly ILease _innerLease;
        private readonly IFaultDecisionPolicy? _renewPolicy;
        private readonly IFaultDecisionPolicy? _releasePolicy;
        private readonly IChaosObserver? _observer;
        private readonly string _providerName;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ChaosLease class.
        /// </summary>
        /// <param name="innerLease">The actual lease instance to wrap.</param>
        /// <param name="renewPolicy">Fault decision policy for RenewAsync operations (optional).</param>
        /// <param name="releasePolicy">Fault decision policy for ReleaseAsync operations (optional).</param>
        /// <param name="observer">Observer for chaos events (optional).</param>
        /// <param name="providerName">Provider name for telemetry (optional).</param>
        public ChaosLease(
            ILease innerLease,
            IFaultDecisionPolicy? renewPolicy = null,
            IFaultDecisionPolicy? releasePolicy = null,
            IChaosObserver? observer = null,
            string? providerName = null)
        {
            _innerLease = innerLease ?? throw new ArgumentNullException(nameof(innerLease));
            _renewPolicy = renewPolicy;
            _releasePolicy = releasePolicy;
            _observer = observer;
            _providerName = providerName ?? "ChaosLease";
        }

        /// <inheritdoc/>
        public string LeaseId => _innerLease.LeaseId;

        /// <inheritdoc/>
        public string LeaseName => _innerLease.LeaseName;

        /// <inheritdoc/>
        public DateTimeOffset AcquiredAt => _innerLease.AcquiredAt;

        /// <inheritdoc/>
        public DateTimeOffset ExpiresAt => _innerLease.ExpiresAt;

        /// <inheritdoc/>
        public bool IsAcquired => _innerLease.IsAcquired;

        /// <inheritdoc/>
        public int RenewalCount => _innerLease.RenewalCount;

        /// <inheritdoc/>
        public event EventHandler<LeaseRenewedEventArgs>? LeaseRenewed
        {
            add => _innerLease.LeaseRenewed += value;
            remove => _innerLease.LeaseRenewed -= value;
        }

        /// <inheritdoc/>
        public event EventHandler<LeaseRenewalFailedEventArgs>? LeaseRenewalFailed
        {
            add => _innerLease.LeaseRenewalFailed += value;
            remove => _innerLease.LeaseRenewalFailed -= value;
        }

        /// <inheritdoc/>
        public event EventHandler<LeaseLostEventArgs>? LeaseLost
        {
            add => _innerLease.LeaseLost += value;
            remove => _innerLease.LeaseLost -= value;
        }

        /// <inheritdoc/>
        public async Task RenewAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var context = CreateFaultContext("RenewAsync");
            context.Metadata["RenewalCount"] = RenewalCount;
            context.Metadata["IsAcquired"] = IsAcquired;
            context.Metadata["ExpiresAt"] = ExpiresAt;

            await MaybeInjectFaultAsync(context, _renewPolicy, cancellationToken).ConfigureAwait(false);
            await _innerLease.RenewAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ReleaseAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var context = CreateFaultContext("ReleaseAsync");
            context.Metadata["RenewalCount"] = RenewalCount;
            context.Metadata["IsAcquired"] = IsAcquired;

            await MaybeInjectFaultAsync(context, _releasePolicy, cancellationToken).ConfigureAwait(false);
            await _innerLease.ReleaseAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await _innerLease.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private FaultContext CreateFaultContext(string operation)
        {
            return new FaultContext
            {
                Operation = operation,
                LeaseName = LeaseName,
                LeaseId = LeaseId,
                Timestamp = DateTimeOffset.UtcNow,
                ProviderName = _providerName,
                Metadata =
                {
                    ["AcquiredAt"] = AcquiredAt,
                    ["ExpiresAt"] = ExpiresAt,
                    ["IsAcquired"] = IsAcquired,
                    ["RenewalCount"] = RenewalCount
                }
            };
        }

        private async Task MaybeInjectFaultAsync(
            FaultContext context,
            IFaultDecisionPolicy? policy,
            CancellationToken cancellationToken)
        {
            if (policy == null)
            {
                _observer?.OnFaultSkipped(context, "No policy configured for operation");
                return;
            }

            var decision = policy.Evaluate(context);
            _observer?.OnFaultDecisionMade(context, decision, policy.Name);

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

                await strategy.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

                var duration = DateTimeOffset.UtcNow - startTime;
                _observer?.OnFaultExecuted(context, strategy, duration);
            }
            catch (Exception ex)
            {
                _observer?.OnFaultExecutionFailed(context, strategy, ex);
                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChaosLease));
            }
        }
    }
}
