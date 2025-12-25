using DistributedLeasing.Abstractions.Configuration;

namespace DistributedLeasing.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating LeaseOptions instances in tests.
/// </summary>
public class LeaseOptionsBuilder
{
    private TimeSpan _duration = TestConstants.LeaseDurations.Medium;
    private bool _autoRenew = false;
    private TimeSpan? _autoRenewInterval;
    private TimeSpan _autoRenewRetryInterval = TestConstants.RetryIntervals.Standard;
    private int _autoRenewMaxRetries = TestConstants.MaxRetries.Few;
    private double _autoRenewSafetyThreshold = TestConstants.SafetyThresholds.Standard;
    private TimeSpan _acquireTimeout = Timeout.InfiniteTimeSpan;
    private TimeSpan _acquireRetryInterval = TestConstants.RetryIntervals.Standard;

    /// <summary>
    /// Creates a new instance of the builder with default values.
    /// </summary>
    /// <returns>A new LeaseOptionsBuilder instance.</returns>
    public static LeaseOptionsBuilder Default() => new();

    /// <summary>
    /// Sets the lease duration.
    /// </summary>
    /// <param name="duration">The lease duration.</param>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder WithDuration(TimeSpan duration)
    {
        _duration = duration;
        return this;
    }

    /// <summary>
    /// Enables auto-renewal with the specified interval.
    /// </summary>
    /// <param name="interval">Optional renewal interval. If null, uses 2/3 of duration.</param>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder WithAutoRenew(TimeSpan? interval = null)
    {
        _autoRenew = true;
        _autoRenewInterval = interval;
        return this;
    }

    /// <summary>
    /// Disables auto-renewal.
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder WithoutAutoRenew()
    {
        _autoRenew = false;
        return this;
    }

    /// <summary>
    /// Sets the acquisition timeout.
    /// </summary>
    /// <param name="timeout">The acquisition timeout.</param>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder WithAcquireTimeout(TimeSpan timeout)
    {
        _acquireTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Configures retry policy for lease acquisition.
    /// </summary>
    /// <param name="retryInterval">Interval between retries.</param>
    /// <param name="maxRetries">Maximum number of retries.</param>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder WithRetryPolicy(TimeSpan retryInterval, int maxRetries)
    {
        _acquireRetryInterval = retryInterval;
        _autoRenewMaxRetries = maxRetries;
        _autoRenewRetryInterval = retryInterval;
        return this;
    }

    /// <summary>
    /// Sets the safety threshold for auto-renewal.
    /// </summary>
    /// <param name="threshold">Safety threshold (0.5-0.95).</param>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder WithSafetyThreshold(double threshold)
    {
        _autoRenewSafetyThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Configures options for high-performance scenarios (short duration, aggressive renewal).
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder AsHighPerformance()
    {
        _duration = TestConstants.LeaseDurations.Short;
        _autoRenew = true;
        _autoRenewInterval = TimeSpan.FromSeconds(10);
        _autoRenewRetryInterval = TestConstants.RetryIntervals.Fast;
        _autoRenewSafetyThreshold = TestConstants.SafetyThresholds.Aggressive;
        _acquireTimeout = TestConstants.Timeouts.Fast;
        return this;
    }

    /// <summary>
    /// Configures options for resilient scenarios (aggressive retries, conservative thresholds).
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public LeaseOptionsBuilder AsResilient()
    {
        _duration = TestConstants.LeaseDurations.Long;
        _autoRenew = true;
        _autoRenewMaxRetries = TestConstants.MaxRetries.Many;
        _autoRenewRetryInterval = TestConstants.RetryIntervals.Fast;
        _autoRenewSafetyThreshold = TestConstants.SafetyThresholds.Conservative;
        _acquireTimeout = TestConstants.Timeouts.Extended;
        return this;
    }

    /// <summary>
    /// Builds the LeaseOptions instance.
    /// </summary>
    /// <returns>A configured LeaseOptions instance.</returns>
    public LeaseOptions Build()
    {
        var options = new LeaseOptions
        {
            DefaultLeaseDuration = _duration,
            AutoRenew = _autoRenew,
            AutoRenewRetryInterval = _autoRenewRetryInterval,
            AutoRenewMaxRetries = _autoRenewMaxRetries,
            AutoRenewSafetyThreshold = _autoRenewSafetyThreshold,
            AcquireTimeout = _acquireTimeout,
            AcquireRetryInterval = _acquireRetryInterval
        };

        if (_autoRenewInterval.HasValue)
        {
            options.AutoRenewInterval = _autoRenewInterval.Value;
        }

        return options;
    }

    /// <summary>
    /// Implicitly converts the builder to LeaseOptions by calling Build().
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    public static implicit operator LeaseOptions(LeaseOptionsBuilder builder) => builder.Build();
}
