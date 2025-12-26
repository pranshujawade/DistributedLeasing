using System;
using System.Collections.Generic;
using System.Linq;

namespace DistributedLeasing.ChaosEngineering.Configuration
{
    /// <summary>
    /// Validates ChaosOptions configuration to ensure correctness and safety.
    /// Implements fail-fast validation to catch configuration errors early.
    /// </summary>
    public class ChaosOptionsValidator
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        /// <summary>
        /// Gets the collection of validation errors.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors.AsReadOnly();

        /// <summary>
        /// Gets the collection of validation warnings.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

        /// <summary>
        /// Gets whether the validation passed (no errors).
        /// </summary>
        public bool IsValid => _errors.Count == 0;

        /// <summary>
        /// Validates the provided ChaosOptions configuration.
        /// </summary>
        /// <param name="options">The options to validate.</param>
        /// <returns>This validator instance for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown if options is null.</exception>
        public ChaosOptionsValidator Validate(ChaosOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _errors.Clear();
            _warnings.Clear();

            ValidateGlobalSettings(options);
            ValidateRateLimiting(options);
            ValidateFaultStrategies(options);
            ValidateDefaultPolicy(options);
            ValidateOperationOptions(options);
            ValidateEnvironmentTags(options);

            return this;
        }

        /// <summary>
        /// Throws an exception if validation failed, or logs warnings if validation passed with warnings.
        /// </summary>
        /// <exception cref="ChaosConfigurationException">Thrown if validation errors exist.</exception>
        public void ThrowIfInvalid()
        {
            if (!IsValid)
            {
                var errorMessage = string.Join(Environment.NewLine, _errors);
                throw new ChaosConfigurationException(
                    $"Chaos configuration validation failed with {_errors.Count} error(s):{Environment.NewLine}{errorMessage}");
            }
        }

        /// <summary>
        /// Gets a summary of validation results.
        /// </summary>
        public string GetValidationSummary()
        {
            if (IsValid && _warnings.Count == 0)
            {
                return "Chaos configuration validation passed with no errors or warnings.";
            }

            var summary = new List<string>();

            if (_errors.Count > 0)
            {
                summary.Add($"Errors ({_errors.Count}):");
                summary.AddRange(_errors.Select(e => $"  - {e}"));
            }

            if (_warnings.Count > 0)
            {
                summary.Add($"Warnings ({_warnings.Count}):");
                summary.AddRange(_warnings.Select(w => $"  - {w}"));
            }

            return string.Join(Environment.NewLine, summary);
        }

        private void ValidateGlobalSettings(ChaosOptions options)
        {
            // Seed validation
            if (options.Seed.HasValue && options.Seed.Value < 0)
            {
                _errors.Add($"Seed must be non-negative. Current value: {options.Seed.Value}");
            }

            // Provider name validation
            if (string.IsNullOrWhiteSpace(options.ProviderName))
            {
                _warnings.Add("ProviderName is not set. Telemetry will not include provider identification.");
            }

            // Enabled state warning
            if (!options.Enabled)
            {
                _warnings.Add("Chaos engineering is globally disabled. No faults will be injected.");
            }
        }

        private void ValidateRateLimiting(ChaosOptions options)
        {
            if (options.MaxFaultRate.HasValue)
            {
                if (options.MaxFaultRate.Value <= 0)
                {
                    _errors.Add($"MaxFaultRate must be positive. Current value: {options.MaxFaultRate.Value}");
                }

                if (options.MaxFaultRate.Value > 1000)
                {
                    _warnings.Add($"MaxFaultRate is very high ({options.MaxFaultRate.Value} faults/sec). This may overwhelm the system.");
                }
            }

            if (options.RateLimitWindowSeconds <= 0)
            {
                _errors.Add($"RateLimitWindowSeconds must be positive. Current value: {options.RateLimitWindowSeconds}");
            }

            if (options.RateLimitWindowSeconds > 3600)
            {
                _warnings.Add($"RateLimitWindowSeconds is very large ({options.RateLimitWindowSeconds} seconds). Consider a smaller window.");
            }
        }

        private void ValidateFaultStrategies(ChaosOptions options)
        {
            if (options.FaultStrategies == null)
            {
                _errors.Add("FaultStrategies collection cannot be null.");
                return;
            }

            if (options.Enabled && options.FaultStrategies.Count == 0)
            {
                _warnings.Add("No global fault strategies configured. Chaos may not inject any faults unless operation-specific strategies are defined.");
            }

            var strategyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var strategy in options.FaultStrategies)
            {
                if (strategy == null)
                {
                    _errors.Add("FaultStrategies collection contains null strategy.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(strategy.Name))
                {
                    _errors.Add("Fault strategy has null or empty Name.");
                }
                else if (!strategyNames.Add(strategy.Name))
                {
                    _errors.Add($"Duplicate fault strategy name: '{strategy.Name}'");
                }
            }
        }

        private void ValidateDefaultPolicy(ChaosOptions options)
        {
            if (options.Enabled && options.DefaultPolicy == null && options.OperationOptions.Count == 0)
            {
                _warnings.Add("No default policy configured and no operation-specific policies defined. Chaos may not inject any faults.");
            }

            if (options.DefaultPolicy != null && string.IsNullOrWhiteSpace(options.DefaultPolicy.Name))
            {
                _errors.Add("DefaultPolicy has null or empty Name.");
            }
        }

        private void ValidateOperationOptions(ChaosOptions options)
        {
            if (options.OperationOptions == null)
            {
                _errors.Add("OperationOptions dictionary cannot be null.");
                return;
            }

            foreach (var kvp in options.OperationOptions)
            {
                var operationName = kvp.Key;
                var operationOptions = kvp.Value;

                if (string.IsNullOrWhiteSpace(operationName))
                {
                    _errors.Add("OperationOptions contains entry with null or empty operation name.");
                    continue;
                }

                if (operationOptions == null)
                {
                    _errors.Add($"Operation '{operationName}' has null configuration.");
                    continue;
                }

                ValidateOperationChaosOptions(operationName, operationOptions);
            }
        }

        private void ValidateOperationChaosOptions(string operationName, OperationChaosOptions options)
        {
            // Rate limiting
            if (options.MaxFaultRate.HasValue && options.MaxFaultRate.Value <= 0)
            {
                _errors.Add($"Operation '{operationName}': MaxFaultRate must be positive. Current value: {options.MaxFaultRate.Value}");
            }

            // Fault strategies
            if (options.FaultStrategies != null)
            {
                var strategyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var strategy in options.FaultStrategies)
                {
                    if (strategy == null)
                    {
                        _errors.Add($"Operation '{operationName}': FaultStrategies collection contains null strategy.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(strategy.Name))
                    {
                        _errors.Add($"Operation '{operationName}': Fault strategy has null or empty Name.");
                    }
                    else if (!strategyNames.Add(strategy.Name))
                    {
                        _errors.Add($"Operation '{operationName}': Duplicate fault strategy name: '{strategy.Name}'");
                    }
                }
            }

            // Policy
            if (options.Policy != null && string.IsNullOrWhiteSpace(options.Policy.Name))
            {
                _errors.Add($"Operation '{operationName}': Policy has null or empty Name.");
            }

            // Lease name patterns
            if (options.LeaseNamePatterns != null)
            {
                foreach (var pattern in options.LeaseNamePatterns)
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                    {
                        _errors.Add($"Operation '{operationName}': LeaseNamePatterns contains null or empty pattern.");
                    }
                }
            }

            // Conditions
            if (options.Conditions != null)
            {
                ValidateOperationConditions(operationName, options.Conditions);
            }
        }

        private void ValidateOperationConditions(string operationName, OperationConditions conditions)
        {
            if (conditions.MinimumAttemptNumber.HasValue && conditions.MinimumAttemptNumber.Value < 1)
            {
                _errors.Add($"Operation '{operationName}': MinimumAttemptNumber must be at least 1. Current value: {conditions.MinimumAttemptNumber.Value}");
            }

            if (conditions.MaximumAttemptNumber.HasValue && conditions.MaximumAttemptNumber.Value < 1)
            {
                _errors.Add($"Operation '{operationName}': MaximumAttemptNumber must be at least 1. Current value: {conditions.MaximumAttemptNumber.Value}");
            }

            if (conditions.MinimumAttemptNumber.HasValue && 
                conditions.MaximumAttemptNumber.HasValue &&
                conditions.MinimumAttemptNumber.Value > conditions.MaximumAttemptNumber.Value)
            {
                _errors.Add($"Operation '{operationName}': MinimumAttemptNumber ({conditions.MinimumAttemptNumber.Value}) cannot be greater than MaximumAttemptNumber ({conditions.MaximumAttemptNumber.Value})");
            }

            if (conditions.TimeConditions != null)
            {
                ValidateTimeConditions(operationName, conditions.TimeConditions);
            }
        }

        private void ValidateTimeConditions(string operationName, TimeConditions timeConditions)
        {
            if (timeConditions.StartTime.HasValue && timeConditions.EndTime.HasValue)
            {
                if (timeConditions.StartTime.Value >= timeConditions.EndTime.Value)
                {
                    _errors.Add($"Operation '{operationName}': StartTime ({timeConditions.StartTime.Value}) must be before EndTime ({timeConditions.EndTime.Value})");
                }
            }

            if (timeConditions.StartDate.HasValue && timeConditions.EndDate.HasValue)
            {
                if (timeConditions.StartDate.Value >= timeConditions.EndDate.Value)
                {
                    _errors.Add($"Operation '{operationName}': StartDate ({timeConditions.StartDate.Value:yyyy-MM-dd}) must be before EndDate ({timeConditions.EndDate.Value:yyyy-MM-dd})");
                }
            }

            if (timeConditions.AllowedDaysOfWeek != null && timeConditions.AllowedDaysOfWeek.Count == 0)
            {
                _warnings.Add($"Operation '{operationName}': AllowedDaysOfWeek is empty. No faults will be injected based on day-of-week conditions.");
            }
        }

        private void ValidateEnvironmentTags(ChaosOptions options)
        {
            if (options.EnvironmentTags == null)
            {
                _errors.Add("EnvironmentTags dictionary cannot be null.");
                return;
            }

            foreach (var kvp in options.EnvironmentTags)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    _errors.Add("EnvironmentTags contains entry with null or empty key.");
                }

                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    _warnings.Add($"EnvironmentTag '{kvp.Key}' has null or empty value.");
                }
            }
        }
    }

    /// <summary>
    /// Exception thrown when chaos configuration validation fails.
    /// </summary>
    public class ChaosConfigurationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the ChaosConfigurationException class.
        /// </summary>
        public ChaosConfigurationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ChaosConfigurationException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ChaosConfigurationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ChaosConfigurationException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ChaosConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
