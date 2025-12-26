using System;
using System.Collections.Generic;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Configuration
{
    /// <summary>
    /// Fluent builder for constructing ChaosOptions configurations.
    /// Provides a convenient, type-safe way to build chaos configurations programmatically.
    /// </summary>
    public class ChaosOptionsBuilder
    {
        private readonly ChaosOptions _options = new ChaosOptions();

        /// <summary>
        /// Enables chaos engineering globally.
        /// </summary>
        public ChaosOptionsBuilder Enable()
        {
            _options.Enabled = true;
            return this;
        }

        /// <summary>
        /// Disables chaos engineering globally.
        /// </summary>
        public ChaosOptionsBuilder Disable()
        {
            _options.Enabled = false;
            return this;
        }

        /// <summary>
        /// Sets the random seed for reproducible chaos scenarios.
        /// </summary>
        /// <param name="seed">The seed value.</param>
        public ChaosOptionsBuilder WithSeed(int seed)
        {
            if (seed < 0)
            {
                throw new ArgumentException("Seed must be non-negative.", nameof(seed));
            }

            _options.Seed = seed;
            return this;
        }

        /// <summary>
        /// Sets the provider name for telemetry tagging.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        public ChaosOptionsBuilder WithProviderName(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            }

            _options.ProviderName = providerName;
            return this;
        }

        /// <summary>
        /// Sets the default fault decision policy.
        /// </summary>
        /// <param name="policy">The default policy.</param>
        public ChaosOptionsBuilder WithDefaultPolicy(IFaultDecisionPolicy policy)
        {
            _options.DefaultPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        /// <summary>
        /// Adds a fault strategy to the global fault strategies collection.
        /// </summary>
        /// <param name="strategy">The fault strategy to add.</param>
        public ChaosOptionsBuilder AddFaultStrategy(IFaultStrategy strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            _options.FaultStrategies.Add(strategy);
            return this;
        }

        /// <summary>
        /// Adds multiple fault strategies to the global fault strategies collection.
        /// </summary>
        /// <param name="strategies">The fault strategies to add.</param>
        public ChaosOptionsBuilder AddFaultStrategies(params IFaultStrategy[] strategies)
        {
            if (strategies == null)
            {
                throw new ArgumentNullException(nameof(strategies));
            }

            foreach (var strategy in strategies)
            {
                AddFaultStrategy(strategy);
            }

            return this;
        }

        /// <summary>
        /// Sets the global maximum fault injection rate (faults per second).
        /// </summary>
        /// <param name="maxFaultRate">The maximum fault rate.</param>
        public ChaosOptionsBuilder WithMaxFaultRate(double maxFaultRate)
        {
            if (maxFaultRate <= 0)
            {
                throw new ArgumentException("Max fault rate must be positive.", nameof(maxFaultRate));
            }

            _options.MaxFaultRate = maxFaultRate;
            return this;
        }

        /// <summary>
        /// Sets the rate limiting time window in seconds.
        /// </summary>
        /// <param name="windowSeconds">The time window in seconds.</param>
        public ChaosOptionsBuilder WithRateLimitWindow(int windowSeconds)
        {
            if (windowSeconds <= 0)
            {
                throw new ArgumentException("Rate limit window must be positive.", nameof(windowSeconds));
            }

            _options.RateLimitWindowSeconds = windowSeconds;
            return this;
        }

        /// <summary>
        /// Enables detailed observability (metrics, tracing, logging).
        /// </summary>
        public ChaosOptionsBuilder EnableObservability()
        {
            _options.EnableObservability = true;
            return this;
        }

        /// <summary>
        /// Disables detailed observability.
        /// </summary>
        public ChaosOptionsBuilder DisableObservability()
        {
            _options.EnableObservability = false;
            return this;
        }

        /// <summary>
        /// Sets whether to fail fast on configuration errors.
        /// </summary>
        /// <param name="failFast">True to fail fast, false to log warnings.</param>
        public ChaosOptionsBuilder WithFailFast(bool failFast)
        {
            _options.FailFastOnConfigurationErrors = failFast;
            return this;
        }

        /// <summary>
        /// Sets the minimum severity level for fault injection.
        /// </summary>
        /// <param name="minimumSeverity">The minimum severity level.</param>
        public ChaosOptionsBuilder WithMinimumSeverity(FaultSeverity minimumSeverity)
        {
            _options.MinimumSeverity = minimumSeverity;
            return this;
        }

        /// <summary>
        /// Adds a global metadata entry.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        public ChaosOptionsBuilder AddGlobalMetadata(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Metadata key cannot be null or whitespace.", nameof(key));
            }

            _options.GlobalMetadata[key] = value;
            return this;
        }

        /// <summary>
        /// Adds an environment tag.
        /// </summary>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The tag value.</param>
        public ChaosOptionsBuilder AddEnvironmentTag(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Tag key cannot be null or whitespace.", nameof(key));
            }

            _options.EnvironmentTags[key] = value;
            return this;
        }

        /// <summary>
        /// Configures operation-specific chaos options.
        /// </summary>
        /// <param name="operationName">The operation name (e.g., "AcquireAsync").</param>
        /// <param name="configure">Configuration action for the operation options.</param>
        public ChaosOptionsBuilder ConfigureOperation(string operationName, Action<OperationChaosOptionsBuilder> configure)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name cannot be null or whitespace.", nameof(operationName));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = new OperationChaosOptionsBuilder();
            configure(builder);
            _options.OperationOptions[operationName] = builder.Build();
            return this;
        }

        /// <summary>
        /// Builds the ChaosOptions configuration.
        /// </summary>
        /// <param name="validate">Whether to validate the configuration before returning.</param>
        /// <returns>The configured ChaosOptions.</returns>
        /// <exception cref="ChaosConfigurationException">Thrown if validation fails and validate is true.</exception>
        public ChaosOptions Build(bool validate = true)
        {
            if (validate)
            {
                var validator = new ChaosOptionsValidator();
                validator.Validate(_options).ThrowIfInvalid();
            }

            return _options;
        }
    }

    /// <summary>
    /// Fluent builder for constructing OperationChaosOptions.
    /// </summary>
    public class OperationChaosOptionsBuilder
    {
        private readonly OperationChaosOptions _options = new OperationChaosOptions();

        /// <summary>
        /// Enables chaos for this operation.
        /// </summary>
        public OperationChaosOptionsBuilder Enable()
        {
            _options.Enabled = true;
            return this;
        }

        /// <summary>
        /// Disables chaos for this operation.
        /// </summary>
        public OperationChaosOptionsBuilder Disable()
        {
            _options.Enabled = false;
            return this;
        }

        /// <summary>
        /// Sets the fault decision policy for this operation.
        /// </summary>
        public OperationChaosOptionsBuilder WithPolicy(IFaultDecisionPolicy policy)
        {
            _options.Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        /// <summary>
        /// Adds a fault strategy for this operation.
        /// </summary>
        public OperationChaosOptionsBuilder AddFaultStrategy(IFaultStrategy strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            _options.FaultStrategies.Add(strategy);
            return this;
        }

        /// <summary>
        /// Sets the maximum fault injection rate for this operation.
        /// </summary>
        public OperationChaosOptionsBuilder WithMaxFaultRate(double maxFaultRate)
        {
            if (maxFaultRate <= 0)
            {
                throw new ArgumentException("Max fault rate must be positive.", nameof(maxFaultRate));
            }

            _options.MaxFaultRate = maxFaultRate;
            return this;
        }

        /// <summary>
        /// Sets the minimum severity level for this operation.
        /// </summary>
        public OperationChaosOptionsBuilder WithMinimumSeverity(FaultSeverity minimumSeverity)
        {
            _options.MinimumSeverity = minimumSeverity;
            return this;
        }

        /// <summary>
        /// Adds a lease name pattern (supports regex).
        /// </summary>
        public OperationChaosOptionsBuilder ForLeasePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("Lease pattern cannot be null or whitespace.", nameof(pattern));
            }

            _options.LeaseNamePatterns.Add(pattern);
            return this;
        }

        /// <summary>
        /// Adds operation-specific metadata.
        /// </summary>
        public OperationChaosOptionsBuilder AddMetadata(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Metadata key cannot be null or whitespace.", nameof(key));
            }

            _options.Metadata[key] = value;
            return this;
        }

        /// <summary>
        /// Configures conditional rules for this operation.
        /// </summary>
        public OperationChaosOptionsBuilder WithConditions(Action<OperationConditionsBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = new OperationConditionsBuilder();
            configure(builder);
            _options.Conditions = builder.Build();
            return this;
        }

        internal OperationChaosOptions Build()
        {
            return _options;
        }
    }

    /// <summary>
    /// Fluent builder for constructing OperationConditions.
    /// </summary>
    public class OperationConditionsBuilder
    {
        private readonly OperationConditions _conditions = new OperationConditions();

        /// <summary>
        /// Only inject faults during retry attempts.
        /// </summary>
        public OperationConditionsBuilder OnlyOnRetry()
        {
            _conditions.OnlyOnRetry = true;
            return this;
        }

        /// <summary>
        /// Sets the minimum attempt number before injecting faults.
        /// </summary>
        public OperationConditionsBuilder FromAttempt(int minimumAttemptNumber)
        {
            if (minimumAttemptNumber < 1)
            {
                throw new ArgumentException("Minimum attempt number must be at least 1.", nameof(minimumAttemptNumber));
            }

            _conditions.MinimumAttemptNumber = minimumAttemptNumber;
            return this;
        }

        /// <summary>
        /// Sets the maximum attempt number for injecting faults.
        /// </summary>
        public OperationConditionsBuilder UntilAttempt(int maximumAttemptNumber)
        {
            if (maximumAttemptNumber < 1)
            {
                throw new ArgumentException("Maximum attempt number must be at least 1.", nameof(maximumAttemptNumber));
            }

            _conditions.MaximumAttemptNumber = maximumAttemptNumber;
            return this;
        }

        /// <summary>
        /// Adds a metadata condition that must match for fault injection.
        /// </summary>
        public OperationConditionsBuilder WithMetadata(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Metadata key cannot be null or whitespace.", nameof(key));
            }

            _conditions.MetadataConditions[key] = value;
            return this;
        }

        /// <summary>
        /// Configures time-based conditions.
        /// </summary>
        public OperationConditionsBuilder WithTimeConditions(Action<TimeConditionsBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = new TimeConditionsBuilder();
            configure(builder);
            _conditions.TimeConditions = builder.Build();
            return this;
        }

        internal OperationConditions Build()
        {
            return _conditions;
        }
    }

    /// <summary>
    /// Fluent builder for constructing TimeConditions.
    /// </summary>
    public class TimeConditionsBuilder
    {
        private readonly TimeConditions _conditions = new TimeConditions();

        /// <summary>
        /// Sets the time-of-day window for fault injection (UTC).
        /// </summary>
        public TimeConditionsBuilder BetweenTimes(TimeSpan startTime, TimeSpan endTime)
        {
            if (startTime >= endTime)
            {
                throw new ArgumentException("Start time must be before end time.");
            }

            _conditions.StartTime = startTime;
            _conditions.EndTime = endTime;
            return this;
        }

        /// <summary>
        /// Sets the date range for fault injection.
        /// </summary>
        public TimeConditionsBuilder BetweenDates(DateTimeOffset startDate, DateTimeOffset endDate)
        {
            if (startDate >= endDate)
            {
                throw new ArgumentException("Start date must be before end date.");
            }

            _conditions.StartDate = startDate;
            _conditions.EndDate = endDate;
            return this;
        }

        /// <summary>
        /// Restricts fault injection to specific days of the week.
        /// </summary>
        public TimeConditionsBuilder OnDaysOfWeek(params DayOfWeek[] daysOfWeek)
        {
            if (daysOfWeek == null || daysOfWeek.Length == 0)
            {
                throw new ArgumentException("At least one day of week must be specified.", nameof(daysOfWeek));
            }

            foreach (var day in daysOfWeek)
            {
                _conditions.AllowedDaysOfWeek.Add(day);
            }

            return this;
        }

        internal TimeConditions Build()
        {
            return _conditions;
        }
    }
}
