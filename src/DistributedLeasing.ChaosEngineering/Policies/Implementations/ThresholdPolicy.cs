using System;
using System.Collections.Generic;
using System.Threading;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Policies.Implementations
{
    /// <summary>
    /// A fault decision policy that uses thresholds to determine when to inject faults.
    /// Supports count-based and time-based thresholds for controlled fault injection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy provides fine-grained control over fault injection timing:
    /// - Inject faults only for the first N operations
    /// - Inject faults only after N operations
    /// - Inject faults only within specific time windows
    /// - Combine multiple threshold conditions
    /// </para>
    /// <para>
    /// Thread-safe through synchronized counter management.
    /// </para>
    /// </remarks>
    public class ThresholdPolicy : IFaultDecisionPolicy
    {
        private readonly IFaultStrategy _strategy;
        private readonly int? _minCount;
        private readonly int? _maxCount;
        private readonly DateTimeOffset? _startTime;
        private readonly DateTimeOffset? _endTime;
        private int _evaluationCount;
        private readonly DateTimeOffset _createdAt;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="ThresholdPolicy"/> class.
        /// </summary>
        /// <param name="strategy">The fault strategy to inject when thresholds are met.</param>
        /// <param name="minCount">
        /// Minimum number of evaluations before faults are injected (inclusive).
        /// If null, no minimum threshold is applied.
        /// </param>
        /// <param name="maxCount">
        /// Maximum number of evaluations after which faults are no longer injected (exclusive).
        /// If null, no maximum threshold is applied.
        /// </param>
        /// <param name="startTime">
        /// Start time for fault injection window. If null, no start time constraint.
        /// </param>
        /// <param name="endTime">
        /// End time for fault injection window. If null, no end time constraint.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when strategy is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when minCount > maxCount or startTime > endTime.
        /// </exception>
        public ThresholdPolicy(
            IFaultStrategy strategy,
            int? minCount = null,
            int? maxCount = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (minCount.HasValue && maxCount.HasValue && minCount.Value > maxCount.Value)
            {
                throw new ArgumentException("Minimum count cannot be greater than maximum count");
            }

            if (minCount.HasValue && minCount.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minCount), "Minimum count cannot be negative");
            }

            if (maxCount.HasValue && maxCount.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), "Maximum count cannot be negative");
            }

            if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            {
                throw new ArgumentException("Start time cannot be after end time");
            }

            _strategy = strategy;
            _minCount = minCount;
            _maxCount = maxCount;
            _startTime = startTime;
            _endTime = endTime;
            _evaluationCount = 0;
            _createdAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Creates a threshold policy that injects faults only for the first N evaluations.
        /// </summary>
        /// <param name="count">Number of evaluations to inject faults for.</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <returns>A <see cref="ThresholdPolicy"/> configured for first N operations.</returns>
        public static ThresholdPolicy FirstN(int count, IFaultStrategy strategy)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive");
            }

            return new ThresholdPolicy(strategy, minCount: 0, maxCount: count);
        }

        /// <summary>
        /// Creates a threshold policy that injects faults only after N evaluations.
        /// </summary>
        /// <param name="count">Number of evaluations to skip before injecting faults.</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <returns>A <see cref="ThresholdPolicy"/> configured to start after N operations.</returns>
        public static ThresholdPolicy AfterN(int count, IFaultStrategy strategy)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");
            }

            return new ThresholdPolicy(strategy, minCount: count, maxCount: null);
        }

        /// <summary>
        /// Creates a threshold policy that injects faults only within a specific count range.
        /// </summary>
        /// <param name="minCount">Minimum evaluation count (inclusive).</param>
        /// <param name="maxCount">Maximum evaluation count (exclusive).</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <returns>A <see cref="ThresholdPolicy"/> configured for a count range.</returns>
        public static ThresholdPolicy BetweenCounts(int minCount, int maxCount, IFaultStrategy strategy)
        {
            return new ThresholdPolicy(strategy, minCount: minCount, maxCount: maxCount);
        }

        /// <summary>
        /// Creates a threshold policy that injects faults only within a time window.
        /// </summary>
        /// <param name="duration">Duration of the fault injection window from now.</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <returns>A <see cref="ThresholdPolicy"/> configured for a time window.</returns>
        public static ThresholdPolicy ForDuration(TimeSpan duration, IFaultStrategy strategy)
        {
            var now = DateTimeOffset.UtcNow;
            return new ThresholdPolicy(strategy, startTime: now, endTime: now + duration);
        }

        /// <inheritdoc/>
        public string Name
        {
            get
            {
                var parts = new List<string>();
                
                if (_minCount.HasValue)
                    parts.Add($"min={_minCount}");
                if (_maxCount.HasValue)
                    parts.Add($"max={_maxCount}");
                if (_startTime.HasValue || _endTime.HasValue)
                    parts.Add("time-windowed");

                return $"Threshold ({string.Join(", ", parts)})";
            }
        }

        /// <inheritdoc/>
        public FaultDecision Evaluate(FaultContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            int currentCount;
            
            // Thread-safe counter increment
            lock (_lock)
            {
                currentCount = _evaluationCount;
                _evaluationCount++;
            }

            var now = DateTimeOffset.UtcNow;
            bool withinCountThreshold = IsWithinCountThreshold(currentCount);
            bool withinTimeThreshold = IsWithinTimeThreshold(now);
            bool shouldInject = withinCountThreshold && withinTimeThreshold;

            // Build detailed reason
            var reasons = new List<string>();
            if (_minCount.HasValue || _maxCount.HasValue)
            {
                reasons.Add($"count={currentCount} (min={_minCount?.ToString() ?? "none"}, max={_maxCount?.ToString() ?? "none"})");
            }
            if (_startTime.HasValue || _endTime.HasValue)
            {
                var elapsed = now - _createdAt;
                reasons.Add($"time={elapsed.TotalSeconds:F1}s");
            }

            var metadata = new Dictionary<string, object>
            {
                ["EvaluationCount"] = currentCount,
                ["WithinCountThreshold"] = withinCountThreshold,
                ["WithinTimeThreshold"] = withinTimeThreshold
            };

            if (shouldInject)
            {
                return new FaultDecision
                {
                    ShouldInjectFault = true,
                    FaultStrategy = _strategy,
                    Reason = $"Threshold policy triggered: {string.Join(", ", reasons)}",
                    Metadata = metadata
                };
            }

            return new FaultDecision
            {
                ShouldInjectFault = false,
                Reason = $"Threshold policy skipped: {string.Join(", ", reasons)}",
                Metadata = metadata
            };
        }

        /// <summary>
        /// Determines if the current count is within the configured count threshold.
        /// </summary>
        private bool IsWithinCountThreshold(int count)
        {
            if (_minCount.HasValue && count < _minCount.Value)
                return false;

            if (_maxCount.HasValue && count >= _maxCount.Value)
                return false;

            return true;
        }

        /// <summary>
        /// Determines if the current time is within the configured time threshold.
        /// </summary>
        private bool IsWithinTimeThreshold(DateTimeOffset now)
        {
            if (_startTime.HasValue && now < _startTime.Value)
                return false;

            if (_endTime.HasValue && now >= _endTime.Value)
                return false;

            return true;
        }

        /// <summary>
        /// Resets the evaluation counter.
        /// Useful for restarting test scenarios.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _evaluationCount = 0;
            }
        }

        /// <summary>
        /// Gets the current evaluation count.
        /// </summary>
        public int EvaluationCount
        {
            get
            {
                lock (_lock)
                {
                    return _evaluationCount;
                }
            }
        }
    }
}
