using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Core;

namespace DistributedLeasing.Abstractions
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
    /// </remarks>
    public abstract class LeaseBase : ILease
    {
        private readonly object _lock = new object();
        private bool _isDisposed;
        private DateTimeOffset _expiresAt;

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
        {
            if (string.IsNullOrEmpty(leaseId))
                throw new ArgumentNullException(nameof(leaseId));
            if (string.IsNullOrEmpty(leaseName))
                throw new ArgumentNullException(nameof(leaseName));
            if (duration <= TimeSpan.Zero && duration != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero or infinite.");

            LeaseId = leaseId;
            LeaseName = leaseName;
            AcquiredAt = acquiredAt;
            _expiresAt = duration == Timeout.InfiniteTimeSpan 
                ? DateTimeOffset.MaxValue 
                : acquiredAt + duration;
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
        public async Task RenewAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!IsAcquired)
            {
                throw new Core.Exceptions.LeaseLostException(
                    $"Cannot renew lease '{LeaseName}' because it has expired or been released.")
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }

            await RenewLeaseAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                return; // Already released/disposed

            try
            {
                await ReleaseLeaseAsync(cancellationToken).ConfigureAwait(false);
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
    }
}
