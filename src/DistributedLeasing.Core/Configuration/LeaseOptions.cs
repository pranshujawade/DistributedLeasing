using System;

namespace DistributedLeasing.Core.Configuration
{
    /// <summary>
    /// Base configuration options for lease management.
    /// </summary>
    /// <remarks>
    /// These options apply to all lease providers and define the general behavior
    /// of lease acquisition, renewal, and expiration.
    /// </remarks>
    public class LeaseOptions
    {
        private TimeSpan _defaultLeaseDuration = TimeSpan.FromSeconds(60);
        private TimeSpan _acquireRetryInterval = TimeSpan.FromSeconds(5);
        private TimeSpan _acquireTimeout = Timeout.InfiniteTimeSpan;
        private TimeSpan _autoRenewInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the default duration for acquired leases.
        /// </summary>
        /// <value>
        /// The default lease duration. Default is 60 seconds.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than or equal to zero.
        /// </exception>
        public TimeSpan DefaultLeaseDuration
        {
            get => _defaultLeaseDuration;
            set
            {
                if (value <= TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Lease duration must be greater than zero or Timeout.InfiniteTimeSpan.");
                }
                _defaultLeaseDuration = value;
                
                // Auto-adjust auto-renew interval if not explicitly set
                if (!_autoRenewIntervalSet && value != Timeout.InfiniteTimeSpan)
                {
                    _autoRenewInterval = TimeSpan.FromMilliseconds(value.TotalMilliseconds / 2);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether leases should be automatically renewed.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable automatic renewal; otherwise, <c>false</c>. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// When enabled, acquired leases will be automatically renewed in the background
        /// until explicitly released or disposed. This is useful for leader election and
        /// long-running exclusive operations.
        /// </remarks>
        public bool AutoRenew { get; set; }

        private bool _autoRenewIntervalSet;

        /// <summary>
        /// Gets or sets the interval at which automatic renewal occurs.
        /// </summary>
        /// <value>
        /// The auto-renew interval. Default is half of <see cref="DefaultLeaseDuration"/>.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than or equal to zero.
        /// </exception>
        /// <remarks>
        /// This value should be significantly less than <see cref="DefaultLeaseDuration"/>
        /// to ensure renewal completes before expiration. A value of Duration/2 or Duration/3
        /// is recommended.
        /// </remarks>
        public TimeSpan AutoRenewInterval
        {
            get => _autoRenewInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Auto-renew interval must be greater than zero.");
                }
                _autoRenewInterval = value;
                _autoRenewIntervalSet = true;
            }
        }

        /// <summary>
        /// Gets or sets the maximum time to wait when acquiring a lease using <see cref="ILeaseManager.AcquireAsync"/>.
        /// </summary>
        /// <value>
        /// The acquire timeout. Default is <see cref="Timeout.InfiniteTimeSpan"/> (wait indefinitely).
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than zero and not <see cref="Timeout.InfiniteTimeSpan"/>.
        /// </exception>
        public TimeSpan AcquireTimeout
        {
            get => _acquireTimeout;
            set
            {
                if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Acquire timeout must be non-negative or Timeout.InfiniteTimeSpan.");
                }
                _acquireTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the delay between retry attempts when acquiring a lease.
        /// </summary>
        /// <value>
        /// The retry interval. Default is 5 seconds.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than or equal to zero.
        /// </exception>
        /// <remarks>
        /// This interval applies to the <see cref="ILeaseManager.AcquireAsync"/> method
        /// when waiting for a lease to become available.
        /// </remarks>
        public TimeSpan AcquireRetryInterval
        {
            get => _acquireRetryInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Retry interval must be greater than zero.");
                }
                _acquireRetryInterval = value;
            }
        }

        /// <summary>
        /// Validates the configuration options.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configuration is invalid.
        /// </exception>
        /// <remarks>
        /// Call this method to ensure the configuration is valid before using it
        /// to create a lease manager.
        /// </remarks>
        public virtual void Validate()
        {
            if (AutoRenew && AutoRenewInterval >= DefaultLeaseDuration && DefaultLeaseDuration != Timeout.InfiniteTimeSpan)
            {
                throw new InvalidOperationException(
                    "AutoRenewInterval must be less than DefaultLeaseDuration to ensure renewal before expiration.");
            }
        }
    }
}
