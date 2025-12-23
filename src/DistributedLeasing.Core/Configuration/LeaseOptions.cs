using System;
using Azure.Core;
using DistributedLeasing.Authentication;

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
        private TimeSpan _autoRenewInterval = TimeSpan.FromSeconds(40); // 2/3 of default 60s
        private TimeSpan _autoRenewRetryInterval = TimeSpan.FromSeconds(5);
        private int _autoRenewMaxRetries = 3;
        private double _autoRenewSafetyThreshold = 0.9;

        /// <summary>
        /// Gets or sets the authentication configuration for Azure resources.
        /// </summary>
        /// <value>
        /// An <see cref="AuthenticationOptions"/> instance for configuring authentication, or <c>null</c> to use legacy methods.
        /// </value>
        /// <remarks>
        /// <para>
        /// This is the recommended way to configure authentication. It supports:
        /// <list type="bullet">
        /// <item><description>Auto - Automatic environment-aware credential chain</description></item>
        /// <item><description>ManagedIdentity - Azure Managed Identity</description></item>
        /// <item><description>WorkloadIdentity - Kubernetes/GitHub Actions Workload Identity</description></item>
        /// <item><description>ServicePrincipal - Certificate or Secret-based Service Principal</description></item>
        /// <item><description>FederatedCredential - Federated Identity Credential</description></item>
        /// <item><description>Development - Development credentials (Azure CLI, VS, VS Code)</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Example configuration in appsettings.json:
        /// <code>
        /// {
        ///   "Leasing": {
        ///     "Authentication": {
        ///       "Mode": "Auto",
        ///       "EnableDevelopmentCredentials": true
        ///     }
        ///   }
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public AuthenticationOptions? Authentication { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use Azure Managed Identity for authentication.
        /// </summary>
        /// <value>
        /// <c>true</c> to use DefaultAzureCredential for managed identity authentication;
        /// otherwise, <c>false</c>. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// <strong>DEPRECATED:</strong> This property is obsolete and will be removed in a future version.
        /// Use the <see cref="Authentication"/> property instead.
        /// </para>
        /// <para>
        /// When set to <c>true</c>, the provider will use DefaultAzureCredential which supports
        /// environment variables, workload identity, managed identity, Azure CLI, and Visual Studio authentication.
        /// </para>
        /// <para>
        /// Migration example:
        /// <code>
        /// // OLD:
        /// options.UseManagedIdentity = true;
        /// 
        /// // NEW:
        /// options.Authentication = new AuthenticationOptions
        /// {
        ///     Mode = AuthenticationModes.Auto
        /// };
        /// </code>
        /// </para>
        /// </remarks>
        [Obsolete("Use Authentication property instead. This property will be removed in a future version. See documentation for migration guide.")]
        public bool UseManagedIdentity { get; set; }

        /// <summary>
        /// Gets or sets the Azure credential to use for authentication.
        /// </summary>
        /// <value>
        /// A <see cref="TokenCredential"/> for authentication, or <c>null</c> to use other methods.
        /// </value>
        /// <remarks>
        /// <para>
        /// <strong>DEPRECATED:</strong> This property is obsolete and will be removed in a future version.
        /// Manual credential management is not recommended.
        /// </para>
        /// <para>
        /// <strong>NO MIGRATION PATH:</strong> This property allowed manual credential injection, which is
        /// counter to the design of the new authentication library. The library now handles all credential
        /// creation and management automatically based on configuration.
        /// </para>
        /// <para>
        /// If you need credential customization, configure it through <see cref="Authentication"/> property
        /// using the appropriate authentication mode (ServicePrincipal, FederatedCredential, etc.).
        /// </para>
        /// <para>
        /// For backward compatibility, if this property is set, it will still be used and will take
        /// precedence over all other authentication methods. However, this behavior will be removed
        /// in a future version.
        /// </para>
        /// </remarks>
        [Obsolete("Manual credential injection is deprecated. Use Authentication property instead. This property will be removed in a future version.", false)]
        public TokenCredential? Credential { get; set; }

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
            // Validate authentication configuration if provided
            Authentication?.Validate();

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

        /// <summary>
        /// Validates that at least one authentication method is configured.
        /// </summary>
        /// <param name="hasConnectionString">Whether a connection string is provided.</param>
        /// <param name="hasAlternativeAuth">Whether an alternative authentication method (like access key) is provided.</param>
        /// <param name="providerName">The name of the provider for error messages.</param>
        /// <exception cref="InvalidOperationException">Thrown when no authentication method is configured.</exception>
        protected void ValidateAuthenticationConfigured(bool hasConnectionString, bool hasAlternativeAuth, string providerName)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (Credential == null && !UseManagedIdentity && Authentication == null && !hasConnectionString && !hasAlternativeAuth)
            {
                throw new InvalidOperationException(
                    $"No authentication method configured for {providerName}. " +
                    "Set Authentication, Credential, UseManagedIdentity=true, ConnectionString, or an alternative authentication method.");
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Validates that an endpoint is provided when using credential-based authentication.
        /// </summary>
        /// <param name="hasEndpoint">Whether an endpoint is provided.</param>
        /// <param name="endpointPropertyName">The name of the endpoint property for error messages.</param>
        /// <exception cref="InvalidOperationException">Thrown when endpoint is missing for credential-based authentication.</exception>
        protected void ValidateEndpointForCredential(bool hasEndpoint, string endpointPropertyName = "Endpoint")
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if ((Credential != null || UseManagedIdentity || Authentication != null) && !hasEndpoint)
            {
                throw new InvalidOperationException(
                    $"{endpointPropertyName} is required when using Authentication, Credential, or UseManagedIdentity.");
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
