using System;

namespace DistributedLeasing.Abstractions.Configuration
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
        private TimeSpan _autoRenewInterval = TimeSpan.FromSeconds(40); // 2/3 of default 60s
        private TimeSpan _autoRenewRetryInterval = TimeSpan.FromSeconds(5);
        private int _autoRenewMaxRetries = 3;
        private double _autoRenewSafetyThreshold = 0.9;



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
                
                // Auto-adjust auto-renew interval if not explicitly set (use 2/3 of duration)
                if (!_autoRenewIntervalSet && value != Timeout.InfiniteTimeSpan)
                {
                    _autoRenewInterval = TimeSpan.FromMilliseconds(value.TotalMilliseconds * 2.0 / 3.0);
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
        /// The auto-renew interval. Default is 2/3 of <see cref="DefaultLeaseDuration"/>.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than or equal to zero.
        /// </exception>
        /// <remarks>
        /// This value should be significantly less than <see cref="DefaultLeaseDuration"/>
        /// to ensure renewal completes before expiration. The recommended value is 2/3 of the
        /// lease duration, which provides a 1/3 buffer for retry attempts and error handling.
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
        /// Gets or sets the delay between retry attempts when a lease renewal fails.
        /// </summary>
        /// <value>
        /// The retry interval for auto-renewal. Default is 5 seconds.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than or equal to zero.
        /// </exception>
        /// <remarks>
        /// This interval is used for exponential backoff when renewal attempts fail.
        /// The delay increases with each retry attempt to avoid overwhelming the provider.
        /// </remarks>
        public TimeSpan AutoRenewRetryInterval
        {
            get => _autoRenewRetryInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Auto-renew retry interval must be greater than zero.");
                }
                _autoRenewRetryInterval = value;
            }
        }
        
        /// <summary>
        /// Gets or sets the maximum number of retry attempts for auto-renewal before marking the lease as lost.
        /// </summary>
        /// <value>
        /// The maximum retry attempts. Default is 3.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is less than zero.
        /// </exception>
        /// <remarks>
        /// After this many consecutive renewal failures, the lease will be marked as lost
        /// and the auto-renewal task will stop. Set to 0 to disable retries.
        /// </remarks>
        public int AutoRenewMaxRetries
        {
            get => _autoRenewMaxRetries;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Auto-renew max retries must be non-negative.");
                }
                _autoRenewMaxRetries = value;
            }
        }
        
        /// <summary>
        /// Gets or sets the safety threshold as a fraction of lease duration.
        /// </summary>
        /// <value>
        /// The safety threshold (0.0 to 1.0). Default is 0.9 (90%).
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the value is not between 0.5 and 0.95.
        /// </exception>
        /// <remarks>
        /// Renewal attempts will not be made if the time since acquisition exceeds
        /// this fraction of the lease duration. This prevents renewal attempts that
        /// are too close to expiration. The value must be between 0.5 (50%) and 0.95 (95%).
        /// </remarks>
        public double AutoRenewSafetyThreshold
        {
            get => _autoRenewSafetyThreshold;
            set
            {
                if (value < 0.5 || value > 0.95)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Auto-renew safety threshold must be between 0.5 and 0.95.");
                }
                _autoRenewSafetyThreshold = value;
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
            if (AutoRenew && DefaultLeaseDuration != Timeout.InfiniteTimeSpan)
            {
                if (AutoRenewInterval >= DefaultLeaseDuration)
                {
                    throw new InvalidOperationException(
                        "AutoRenewInterval must be less than DefaultLeaseDuration to ensure renewal before expiration.");
                }
        
                // Validate that renewal interval is not too close to duration
                var safetyDuration = TimeSpan.FromMilliseconds(DefaultLeaseDuration.TotalMilliseconds * AutoRenewSafetyThreshold);
                if (AutoRenewInterval >= safetyDuration)
                {
                    throw new InvalidOperationException(
                        $"AutoRenewInterval ({AutoRenewInterval}) should be less than {AutoRenewSafetyThreshold * 100}% of DefaultLeaseDuration ({DefaultLeaseDuration}) to allow time for retries. " +
                        $"Consider setting AutoRenewInterval to {TimeSpan.FromMilliseconds(DefaultLeaseDuration.TotalMilliseconds * 2.0 / 3.0)} or less.");
                }
        
                // Validate retry interval is reasonable
                var remainingBuffer = DefaultLeaseDuration - AutoRenewInterval;
                if (AutoRenewRetryInterval > remainingBuffer)
                {
                    throw new InvalidOperationException(
                        $"AutoRenewRetryInterval ({AutoRenewRetryInterval}) is too large for the buffer time ({remainingBuffer}) between renewal and expiration. " +
                        $"Consider reducing AutoRenewRetryInterval or increasing the buffer by reducing AutoRenewInterval.");
                }
            }
        }
    }
}
