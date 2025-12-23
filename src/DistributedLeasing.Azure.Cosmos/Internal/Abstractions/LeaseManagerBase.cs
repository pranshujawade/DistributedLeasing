using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Configuration;
using DistributedLeasing.Core.Exceptions;

namespace DistributedLeasing.Azure.Cosmos.Internal.Abstractions
{
    /// <summary>
    /// Base implementation of <see cref="ILeaseManager"/> providing common functionality for all lease managers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This abstract class implements the Template Method pattern, providing the retry and timeout logic
    /// for lease acquisition while delegating the actual storage operations to the <see cref="ILeaseProvider"/>.
    /// </para>
    /// <para>
    /// Derived classes typically only need to configure the provider and options, as the base class
    /// handles all the acquisition retry logic.
    /// </para>
    /// </remarks>
    internal abstract class LeaseManagerBase : ILeaseManager
    {
        /// <summary>
        /// Gets the lease provider that handles storage-specific operations.
        /// </summary>
        protected ILeaseProvider Provider { get; }

        /// <summary>
        /// Gets the configuration options for lease management.
        /// </summary>
        protected LeaseOptions Options { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseManagerBase"/> class.
        /// </summary>
        /// <param name="provider">The lease provider for storage operations.</param>
        /// <param name="options">The configuration options.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="provider"/> or <paramref name="options"/> is null.
        /// </exception>
        protected LeaseManagerBase(ILeaseProvider provider, LeaseOptions options)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            
            // Validate options
            options.Validate();
        }

        /// <inheritdoc/>
        public virtual Task<ILease?> TryAcquireAsync(
            string leaseName,
            TimeSpan? duration = null,
            CancellationToken cancellationToken = default)
        {
            ValidateLeaseName(leaseName);
            
            var effectiveDuration = duration ?? Options.DefaultLeaseDuration;
            ValidateDuration(effectiveDuration);

            return Provider.AcquireLeaseAsync(leaseName, effectiveDuration, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async Task<ILease> AcquireAsync(
            string leaseName,
            TimeSpan? duration = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateLeaseName(leaseName);
            
            var effectiveDuration = duration ?? Options.DefaultLeaseDuration;
            ValidateDuration(effectiveDuration);
            
            var effectiveTimeout = timeout ?? Options.AcquireTimeout;
            ValidateTimeout(effectiveTimeout);

            var startTime = DateTimeOffset.UtcNow;
            var retryInterval = Options.AcquireRetryInterval;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if we've exceeded the timeout
                if (effectiveTimeout != Timeout.InfiniteTimeSpan)
                {
                    var elapsed = DateTimeOffset.UtcNow - startTime;
                    if (elapsed >= effectiveTimeout)
                    {
                        throw new LeaseAcquisitionException(
                            $"Could not acquire lease '{leaseName}' within the specified timeout of {effectiveTimeout}.")
                        {
                            LeaseName = leaseName
                        };
                    }
                }

                // Try to acquire the lease
                try
                {
                    var lease = await Provider.AcquireLeaseAsync(
                        leaseName,
                        effectiveDuration,
                        cancellationToken).ConfigureAwait(false);

                    if (lease != null)
                    {
                        return lease;
                    }
                }
                catch (LeaseConflictException)
                {
                    // Normal competition - continue retrying
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not LeaseException)
                {
                    throw new LeaseAcquisitionException(
                        $"Unexpected error while acquiring lease '{leaseName}'.",
                        ex)
                    {
                        LeaseName = leaseName
                    };
                }

                // Calculate remaining time for this retry
                TimeSpan delayDuration;
                if (effectiveTimeout != Timeout.InfiniteTimeSpan)
                {
                    var elapsed = DateTimeOffset.UtcNow - startTime;
                    var remaining = effectiveTimeout - elapsed;
                    
                    if (remaining <= TimeSpan.Zero)
                    {
                        throw new LeaseAcquisitionException(
                            $"Could not acquire lease '{leaseName}' within the specified timeout of {effectiveTimeout}.")
                        {
                            LeaseName = leaseName
                        };
                    }
                    
                    delayDuration = remaining < retryInterval ? remaining : retryInterval;
                }
                else
                {
                    delayDuration = retryInterval;
                }

                // Wait before retrying
                try
                {
                    await Task.Delay(delayDuration, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Validates the lease name parameter.
        /// </summary>
        /// <param name="leaseName">The lease name to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when the lease name is null or empty.</exception>
        protected virtual void ValidateLeaseName(string leaseName)
        {
            if (string.IsNullOrWhiteSpace(leaseName))
            {
                throw new ArgumentNullException(nameof(leaseName), "Lease name cannot be null or empty.");
            }
        }

        /// <summary>
        /// Validates the lease duration parameter.
        /// </summary>
        /// <param name="duration">The duration to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the duration is invalid for this provider.
        /// </exception>
        protected virtual void ValidateDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero && duration != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    "Duration must be greater than zero or Timeout.InfiniteTimeSpan.");
            }
        }

        /// <summary>
        /// Validates the timeout parameter.
        /// </summary>
        /// <param name="timeout">The timeout to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the timeout is negative and not infinite.
        /// </exception>
        protected virtual void ValidateTimeout(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeout),
                    "Timeout must be non-negative or Timeout.InfiniteTimeSpan.");
            }
        }
    }
}
