using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Configuration;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
#if NET8_0_OR_GREATER
using DistributedLeasing.Abstractions.Observability;
#endif

namespace DistributedLeasing.Abstractions.Core
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
    public abstract class LeaseManagerBase : ILeaseManager
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
        /// Gets or sets the logger for structured logging. If not set, uses NullLogger.
        /// </summary>
        protected ILogger Logger { get; set; } = NullLogger.Instance;

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
        public virtual async Task<ILease?> TryAcquireAsync(
            string leaseName,
            TimeSpan? duration = null,
            CancellationToken cancellationToken = default)
        {
            ValidateLeaseName(leaseName);
            
            var effectiveDuration = duration ?? Options.DefaultLeaseDuration;
            ValidateDuration(effectiveDuration);

#if NET5_0_OR_GREATER
            using var activity = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.TryAcquire);
            activity?.SetTag(LeasingActivitySource.Tags.LeaseName, leaseName);
            activity?.SetTag(LeasingActivitySource.Tags.Provider, Provider.GetType().Name);
            activity?.SetTag(LeasingActivitySource.Tags.Duration, effectiveDuration.TotalSeconds);
            activity?.SetTag(LeasingActivitySource.Tags.AutoRenew, Options.AutoRenew);
#endif

            try
            {
                var lease = await Provider.AcquireLeaseAsync(leaseName, effectiveDuration, cancellationToken).ConfigureAwait(false);
                
#if NET5_0_OR_GREATER
                if (lease != null)
                {
                    activity?.SetTag(LeasingActivitySource.Tags.LeaseId, lease.LeaseId);
                    activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Success);
                }
                else
                {
                    activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.AlreadyHeld);
                }
#endif
                
                return lease;
            }
            catch (Exception ex)
            {
#if NET5_0_OR_GREATER
                activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Failure);
                activity?.SetTag(LeasingActivitySource.Tags.ExceptionType, ex.GetType().Name);
                activity?.SetTag(LeasingActivitySource.Tags.ExceptionMessage, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
#else
                // Variable ex used only in NET5_0_OR_GREATER for tracing
                _ = ex;
#endif
                throw;
            }
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

#if NET5_0_OR_GREATER
            using var activity = LeasingActivitySource.Source.StartActivity(LeasingActivitySource.Operations.Acquire);
            activity?.SetTag(LeasingActivitySource.Tags.LeaseName, leaseName);
            activity?.SetTag(LeasingActivitySource.Tags.Provider, Provider.GetType().Name);
            activity?.SetTag(LeasingActivitySource.Tags.Duration, effectiveDuration.TotalSeconds);
            activity?.SetTag(LeasingActivitySource.Tags.Timeout, effectiveTimeout.TotalSeconds);
            activity?.SetTag(LeasingActivitySource.Tags.AutoRenew, Options.AutoRenew);
#endif

            var startTime = DateTimeOffset.UtcNow;
            var retryInterval = Options.AcquireRetryInterval;
            
            Logger.LogDebug("Attempting to acquire lease '{LeaseName}' with duration {Duration}s and timeout {Timeout}s",
                leaseName, effectiveDuration.TotalSeconds, effectiveTimeout.TotalSeconds);
            
            // Safety valve: even with infinite timeout, limit max attempts to prevent runaway loops
            const int MaxAttemptsWithInfiniteTimeout = 10000;
            int attemptCount = 0;

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Circuit breaker: prevent infinite loops even with Timeout.InfiniteTimeSpan
                    if (effectiveTimeout == Timeout.InfiniteTimeSpan)
                    {
                        if (++attemptCount > MaxAttemptsWithInfiniteTimeout)
                        {
                            throw new LeaseAcquisitionException(
                                $"Could not acquire lease '{leaseName}' after {MaxAttemptsWithInfiniteTimeout} attempts (safety limit).")
                            {
                                LeaseName = leaseName
                            };
                        }
                    }

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
#if NET8_0_OR_GREATER
                        var sw = Stopwatch.StartNew();
#endif
                        var lease = await Provider.AcquireLeaseAsync(
                            leaseName,
                            effectiveDuration,
                            cancellationToken).ConfigureAwait(false);

                        if (lease != null)
                        {
#if NET8_0_OR_GREATER
                            sw.Stop();
                            var providerName = Provider.GetType().Name;
                            LeasingMetrics.LeaseAcquisitions.Add(1, 
                                new("provider", providerName),
                                new("lease_name", leaseName),
                                new("result", "success"));
                            LeasingMetrics.LeaseAcquisitionDuration.Record(sw.Elapsed.TotalMilliseconds,
                                new("provider", providerName),
                                new("result", "success"));
#endif
#if NET5_0_OR_GREATER
                            activity?.SetTag(LeasingActivitySource.Tags.LeaseId, lease.LeaseId);
                            activity?.SetTag(LeasingActivitySource.Tags.Result, LeasingActivitySource.Results.Success);
#endif
                            Logger.LogInformation("Successfully acquired lease '{LeaseName}' with ID '{LeaseId}' after {Elapsed}ms",
                                leaseName, lease.LeaseId, (DateTimeOffset.UtcNow - startTime).TotalMilliseconds);
                            return lease;
                        }
                    }
                    catch (LeaseConflictException)
                    {
#if NET8_0_OR_GREATER
                        var providerName = Provider.GetType().Name;
                        LeasingMetrics.LeaseAcquisitions.Add(1,
                            new("provider", providerName),
                            new("lease_name", leaseName),
                            new("result", "conflict"));
#endif
                        Logger.LogDebug("Lease '{LeaseName}' is currently held, will retry", leaseName);
                        // Normal competition - continue retrying
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (ex is not LeaseException)
                    {
#if NET8_0_OR_GREATER
                        var providerName = Provider.GetType().Name;
                        LeasingMetrics.LeaseAcquisitions.Add(1,
                            new("provider", providerName),
                            new("lease_name", leaseName),
                            new("result", "failure"));
#endif
                        Logger.LogError(ex, "Unexpected error while acquiring lease '{LeaseName}'", leaseName);
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
            catch (Exception ex)
            {
#if NET5_0_OR_GREATER
                activity?.SetTag(LeasingActivitySource.Tags.Result, 
                    ex is LeaseAcquisitionException ? LeasingActivitySource.Results.Timeout : LeasingActivitySource.Results.Failure);
                activity?.SetTag(LeasingActivitySource.Tags.ExceptionType, ex.GetType().Name);
                activity?.SetTag(LeasingActivitySource.Tags.ExceptionMessage, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
#else
                // Variable ex used only in NET5_0_OR_GREATER for tracing
                _ = ex;
#endif
                if (ex is LeaseAcquisitionException)
                {
                    Logger.LogWarning("Failed to acquire lease '{LeaseName}' within timeout of {Timeout}s",
                        leaseName, effectiveTimeout.TotalSeconds);
                }
                else
                {
                    Logger.LogError(ex, "Failed to acquire lease '{LeaseName}' due to error", leaseName);
                }
                throw;
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
