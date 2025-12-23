using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Configuration;
using DistributedLeasing.Core.Events;
using DistributedLeasing.Core.Exceptions;

namespace DistributedLeasing.Azure.Blob.Internal.Abstractions
{
    /// <summary>
    /// Base implementation of <see cref="ILease"/> providing common functionality for all lease types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This abstract class implements the Template Method pattern, providing default implementations
    /// for common lease operations while allowing derived classes to customize specific behaviors.
    /// </para>
    /// <para>
    /// Derived classes must implement the <see cref="RenewLeaseAsync"/> and <see cref="ReleaseLeaseAsync"/>
    /// methods to provide storage-specific renewal and release logic.
    /// </para>
    /// <para>
    /// When auto-renewal is enabled via <see cref="LeaseOptions.AutoRenew"/>, this class manages
    /// a background task that automatically renews the lease at the configured interval.
    /// </para>
    /// </remarks>
    internal abstract class LeaseBase : ILease
    {
        private readonly object _lock = new object();
        private readonly TimeSpan _leaseDuration;
        private readonly LeaseOptions? _options;
        private bool _isDisposed;
        private DateTimeOffset _expiresAt;
        private int _renewalCount;
        
        // Auto-renewal fields
        private CancellationTokenSource? _renewalCancellationTokenSource;
        private Task? _renewalTask;
        private readonly SemaphoreSlim _renewalLock = new SemaphoreSlim(1, 1);
        private int _renewalFailureCount;
        private DateTimeOffset _lastSuccessfulRenewal;

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseBase"/> class.
        /// </summary>
        /// <param name="leaseId">The unique identifier for this lease instance.</param>
        /// <param name="leaseName">The logical name of the resource being leased.</param>
        /// <param name="acquiredAt">The timestamp when the lease was acquired.</param>
        /// <param name="duration">The duration for which the lease is valid.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="leaseId"/> or <paramref name="leaseName"/> is null or empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="duration"/> is less than or equal to zero.
        /// </exception>
        protected LeaseBase(string leaseId, string leaseName, DateTimeOffset acquiredAt, TimeSpan duration)
            : this(leaseId, leaseName, acquiredAt, duration, options: null)
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseBase"/> class with auto-renewal options.
        /// </summary>
        /// <param name="leaseId">The unique identifier for this lease instance.</param>
        /// <param name="leaseName">The logical name of the resource being leased.</param>
        /// <param name="acquiredAt">The timestamp when the lease was acquired.</param>
        /// <param name="duration">The duration for which the lease is valid.</param>
        /// <param name="options">Optional lease configuration including auto-renewal settings.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="leaseId"/> or <paramref name="leaseName"/> is null or empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="duration"/> is less than or equal to zero.
        /// </exception>
        protected LeaseBase(string leaseId, string leaseName, DateTimeOffset acquiredAt, TimeSpan duration, LeaseOptions? options)
        {
            if (string.IsNullOrWhiteSpace(leaseId))
                throw new ArgumentException("Lease ID cannot be null, empty, or whitespace only.", nameof(leaseId));
            if (string.IsNullOrWhiteSpace(leaseName))
                throw new ArgumentException("Lease name cannot be null, empty, or whitespace only.", nameof(leaseName));
            if (duration <= TimeSpan.Zero && duration != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero or infinite.");

            LeaseId = leaseId;
            LeaseName = leaseName;
            AcquiredAt = acquiredAt;
            _leaseDuration = duration;
            _options = options;
            _expiresAt = duration == Timeout.InfiniteTimeSpan 
                ? DateTimeOffset.MaxValue 
                : acquiredAt + duration;
            _lastSuccessfulRenewal = acquiredAt;
            
            // Start auto-renewal if configured
            if (options?.AutoRenew == true && duration != Timeout.InfiniteTimeSpan)
            {
                StartAutoRenewal();
            }
        }

        /// <inheritdoc/>
        public string LeaseId { get; }

        /// <inheritdoc/>
        public string LeaseName { get; }

        /// <inheritdoc/>
        public DateTimeOffset AcquiredAt { get; }

        /// <inheritdoc/>
        public DateTimeOffset ExpiresAt
        {
            get
            {
                lock (_lock)
                {
                    return _expiresAt;
                }
            }
            protected set
            {
                lock (_lock)
                {
                    _expiresAt = value;
                }
            }
        }

        /// <inheritdoc/>
        public virtual bool IsAcquired
        {
            get
            {
                lock (_lock)
                {
                    return !_isDisposed && DateTimeOffset.UtcNow < _expiresAt;
                }
            }
        }
        
        /// <inheritdoc/>
        public int RenewalCount
        {
            get
            {
                lock (_lock)
                {
                    return _renewalCount;
                }
            }
        }
        
        /// <inheritdoc/>
        public event EventHandler<LeaseRenewedEventArgs>? LeaseRenewed;
        
        /// <inheritdoc/>
        public event EventHandler<LeaseRenewalFailedEventArgs>? LeaseRenewalFailed;
        
        /// <inheritdoc/>
        public event EventHandler<LeaseLostEventArgs>? LeaseLost;

        /// <inheritdoc/>
        public async Task RenewAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsAcquired)
            {
                throw new LeaseLostException(
                    $"Cannot renew lease '{LeaseName}' because it has expired or been released.")
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }

            await PerformRenewalAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                return; // Already released/disposed

            // Stop auto-renewal before releasing
            StopAutoRenewal();

            try
            {
                await ReleaseLeaseAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Suppress exceptions to make ReleaseAsync idempotent
                // Lease will expire naturally if release fails
            }
            finally
            {
                _isDisposed = true;
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            // Stop auto-renewal before disposing
            StopAutoRenewal();

            try
            {
                await ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Suppress exceptions during disposal
                // Lease will expire naturally if release fails
            }
            finally
            {
                _isDisposed = true;
                _renewalLock.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// When overridden in a derived class, performs the storage-specific renewal operation.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous renewal operation.</returns>
        /// <remarks>
        /// Implementations should:
        /// <list type="bullet">
        /// <item>Verify ownership of the lease using <see cref="LeaseId"/></item>
        /// <item>Extend the expiration time in the underlying storage</item>
        /// <item>Update <see cref="ExpiresAt"/> on success</item>
        /// <item>Throw <see cref="Core.Exceptions.LeaseRenewalException"/> if renewal fails</item>
        /// </list>
        /// </remarks>
        protected abstract Task RenewLeaseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// When overridden in a derived class, performs the storage-specific release operation.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous release operation.</returns>
        /// <remarks>
        /// Implementations should:
        /// <list type="bullet">
        /// <item>Verify ownership of the lease using <see cref="LeaseId"/></item>
        /// <item>Release the lease in the underlying storage</item>
        /// <item>Be idempotent (safe to call multiple times)</item>
        /// <item>Not throw exceptions for normal release scenarios</item>
        /// </list>
        /// </remarks>
        protected abstract Task ReleaseLeaseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this lease has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the lease has been disposed.</exception>
        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(
                    GetType().Name,
                    $"The lease '{LeaseName}' has been disposed.");
            }
        }
        
        /// <summary>
        /// Starts the background auto-renewal task.
        /// </summary>
        private void StartAutoRenewal()
        {
            if (_options?.AutoRenew != true || _leaseDuration == Timeout.InfiniteTimeSpan)
                return;
                
            _renewalCancellationTokenSource = new CancellationTokenSource();
            _renewalTask = Task.Run(() => AutoRenewalLoopAsync(_renewalCancellationTokenSource.Token), _renewalCancellationTokenSource.Token);
        }
        
        /// <summary>
        /// Stops the background auto-renewal task.
        /// </summary>
        private void StopAutoRenewal(bool waitForCompletion = true)
        {
            if (_renewalCancellationTokenSource != null)
            {
                _renewalCancellationTokenSource.Cancel();
                _renewalCancellationTokenSource.Dispose();
                _renewalCancellationTokenSource = null;
            }
            
            // Wait for renewal task to complete (with timeout)
            // Skip waiting if called from within the renewal task itself to avoid deadlock
            if (waitForCompletion && _renewalTask != null)
            {
                try
                {
                    _renewalTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Ignore cancellation and timeout exceptions
                }
                _renewalTask = null;
            }
        }
        
        /// <summary>
        /// Background loop that handles automatic lease renewal.
        /// </summary>
        private async Task AutoRenewalLoopAsync(CancellationToken cancellationToken)
        {
            var lastRenewalAttempt = AcquiredAt;
            
            while (!cancellationToken.IsCancellationRequested && IsAcquired)
            {
                try
                {
                    // Calculate when to renew
                    var renewalInterval = _options!.AutoRenewInterval;
                    var timeSinceLastRenewal = DateTimeOffset.UtcNow - lastRenewalAttempt;
                    var timeUntilRenewal = renewalInterval - timeSinceLastRenewal;
                    
                    // If it's time to renew or past time, renew immediately
                    if (timeUntilRenewal > TimeSpan.Zero)
                    {
                        await Task.Delay(timeUntilRenewal, cancellationToken).ConfigureAwait(false);
                    }
                    
                    // Update last renewal attempt time
                    lastRenewalAttempt = DateTimeOffset.UtcNow;
                    
                    // Check if we're still within safety threshold
                    var timeSinceAcquisition = DateTimeOffset.UtcNow - AcquiredAt;
                    var safetyThreshold = TimeSpan.FromMilliseconds(_leaseDuration.TotalMilliseconds * _options.AutoRenewSafetyThreshold);
                    
                    if (timeSinceAcquisition >= safetyThreshold)
                    {
                        // Too close to expiration, mark lease as lost
                        OnLeaseLost($"Renewal window exceeded safety threshold ({_options.AutoRenewSafetyThreshold * 100}%)");
                        break;
                    }
                    
                    // Attempt renewal with retry logic
                    await AttemptRenewalWithRetryAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping auto-renewal
                    break;
                }
                catch (Exception ex)
                {
                    // Only raise LeaseLost if it hasn't been raised already
                    // (It may have been raised by AttemptRenewalWithRetryAsync)
                    bool alreadyDisposed;
                    lock (_lock)
                    {
                        alreadyDisposed = _isDisposed;
                    }
                    
                    if (!alreadyDisposed)
                    {
                        OnLeaseLost($"Unexpected error in auto-renewal loop: {ex.Message}");
                    }
                    break;
                }
            }
        }
        
        /// <summary>
        /// Attempts to renew the lease with retry logic.
        /// </summary>
        private async Task AttemptRenewalWithRetryAsync(CancellationToken cancellationToken)
        {
            var maxRetries = _options!.AutoRenewMaxRetries;
            var retryInterval = _options.AutoRenewRetryInterval;
            
            for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
            {
                try
                {
                    await PerformRenewalAsync(cancellationToken).ConfigureAwait(false);
                    
                    // Reset failure count on success
                    _renewalFailureCount = 0;
                    return; // Success!
                }
                catch (LeaseLostException ex)
                {
                    // Lease is definitively lost, stop retrying
                    OnLeaseLost(ex.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    // Transient failure
                    _renewalFailureCount++;
                    
                    var isLastAttempt = attempt >= maxRetries + 1;
                    var willRetry = !isLastAttempt;
                    
                    // Always raise LeaseRenewalFailed for every failed attempt
                    OnLeaseRenewalFailed(attempt, ex, willRetry);
                    
                    if (isLastAttempt)
                    {
                        // Final attempt failed, lease is lost
                        OnLeaseLost($"Renewal failed after {maxRetries} retries: {ex.Message}");
                        throw;
                    }
                    
                    // Not the last attempt, retry with exponential backoff
                    var delay = TimeSpan.FromMilliseconds(retryInterval.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    
                    // Ensure we don't exceed safety threshold with retry
                    var timeSinceAcquisition = DateTimeOffset.UtcNow - AcquiredAt;
                    var safetyThreshold = TimeSpan.FromMilliseconds(_leaseDuration.TotalMilliseconds * _options.AutoRenewSafetyThreshold);
                    var remainingTime = safetyThreshold - timeSinceAcquisition;
                    
                    if (remainingTime <= TimeSpan.Zero)
                    {
                        OnLeaseLost("No time remaining for retry before safety threshold");
                        throw;
                    }
                    
                    delay = delay > remainingTime ? remainingTime : delay;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        
        /// <summary>
        /// Performs the actual renewal operation and raises appropriate events.
        /// </summary>
        private async Task PerformRenewalAsync(CancellationToken cancellationToken)
        {
            await _renewalLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var beforeExpiration = ExpiresAt;
                
                // Call provider-specific renewal
                await RenewLeaseAsync(cancellationToken).ConfigureAwait(false);
                
                var afterExpiration = ExpiresAt;
                
                // Update tracking
                lock (_lock)
                {
                    _renewalCount++;
                    _lastSuccessfulRenewal = DateTimeOffset.UtcNow;
                }
                
                // Raise event
                OnLeaseRenewed(afterExpiration, afterExpiration - beforeExpiration);
            }
            finally
            {
                _renewalLock.Release();
            }
        }
        
        /// <summary>
        /// Raises the LeaseRenewed event.
        /// </summary>
        private void OnLeaseRenewed(DateTimeOffset newExpiration, TimeSpan renewalDuration)
        {
            try
            {
                LeaseRenewed?.Invoke(this, new LeaseRenewedEventArgs(
                    LeaseName,
                    LeaseId,
                    DateTimeOffset.UtcNow,
                    newExpiration,
                    renewalDuration));
            }
            catch
            {
                // Suppress event handler exceptions
            }
        }
        
        /// <summary>
        /// Raises the LeaseRenewalFailed event.
        /// </summary>
        private void OnLeaseRenewalFailed(int attemptNumber, Exception exception, bool willRetry)
        {
            try
            {
                LeaseRenewalFailed?.Invoke(this, new LeaseRenewalFailedEventArgs(
                    LeaseName,
                    LeaseId,
                    DateTimeOffset.UtcNow,
                    attemptNumber,
                    exception,
                    willRetry));
            }
            catch
            {
                // Suppress event handler exceptions
            }
        }
        
        /// <summary>
        /// Raises the LeaseLost event and stops auto-renewal.
        /// </summary>
        private void OnLeaseLost(string reason)
        {
            // Mark as disposed to prevent further operations
            lock (_lock)
            {
                _isDisposed = true;
            }
            
            // Stop auto-renewal without waiting to avoid deadlock when called from renewal task
            StopAutoRenewal(waitForCompletion: false);
            
            try
            {
                LeaseLost?.Invoke(this, new LeaseLostEventArgs(
                    LeaseName,
                    LeaseId,
                    DateTimeOffset.UtcNow,
                    reason,
                    _lastSuccessfulRenewal));
            }
            catch
            {
                // Suppress event handler exceptions
            }
        }
    }
}
