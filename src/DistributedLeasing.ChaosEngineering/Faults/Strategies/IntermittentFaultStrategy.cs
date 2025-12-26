using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Faults.Strategies
{
    /// <summary>
    /// Fault strategy that injects faults based on a repeating pattern.
    /// Provides deterministic fault injection for testing retry logic and resilience.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy uses a boolean pattern to determine when to inject the wrapped fault.
    /// For example, a pattern of [true, false, true] will inject a fault, skip, inject, skip, and repeat.
    /// </para>
    /// <para>
    /// This is useful for testing scenarios like:
    /// - "Fail the first 2 attempts, then succeed"
    /// - "Intermittent network failures (fail every other operation)"
    /// - "Gradual recovery (fail less frequently over time)"
    /// </para>
    /// </remarks>
    public class IntermittentFaultStrategy : FaultStrategyBase
    {
        private readonly bool[] _pattern;
        private readonly IFaultStrategy _wrappedStrategy;
        private int _position;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="IntermittentFaultStrategy"/> class.
        /// </summary>
        /// <param name="pattern">
        /// A boolean array where <c>true</c> means inject fault, <c>false</c> means skip.
        /// The pattern repeats indefinitely.
        /// </param>
        /// <param name="wrappedStrategy">The fault strategy to execute when pattern indicates injection.</param>
        /// <exception cref="ArgumentNullException">Thrown when pattern or wrappedStrategy is null.</exception>
        /// <exception cref="ArgumentException">Thrown when pattern is empty.</exception>
        public IntermittentFaultStrategy(bool[] pattern, IFaultStrategy wrappedStrategy)
        {
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            if (pattern.Length == 0)
            {
                throw new ArgumentException("Pattern cannot be empty", nameof(pattern));
            }

            if (wrappedStrategy == null)
            {
                throw new ArgumentNullException(nameof(wrappedStrategy));
            }

            _pattern = pattern;
            _wrappedStrategy = wrappedStrategy;
            _position = 0;
        }

        /// <summary>
        /// Creates an intermittent fault strategy that fails the first N operations.
        /// </summary>
        /// <param name="failCount">Number of operations to fail before succeeding.</param>
        /// <param name="wrappedStrategy">The fault strategy to execute during failures.</param>
        /// <returns>An <see cref="IntermittentFaultStrategy"/> configured to fail N times.</returns>
        public static IntermittentFaultStrategy FailFirstN(int failCount, IFaultStrategy wrappedStrategy)
        {
            if (failCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(failCount), "Fail count must be positive");
            }

            var pattern = new bool[failCount + 1];
            for (int i = 0; i < failCount; i++)
            {
                pattern[i] = true;
            }
            pattern[failCount] = false;

            return new IntermittentFaultStrategy(pattern, wrappedStrategy);
        }

        /// <summary>
        /// Creates an intermittent fault strategy that fails every Nth operation.
        /// </summary>
        /// <param name="n">Fail every Nth operation (e.g., 2 = every other operation fails).</param>
        /// <param name="wrappedStrategy">The fault strategy to execute during failures.</param>
        /// <returns>An <see cref="IntermittentFaultStrategy"/> configured to fail periodically.</returns>
        public static IntermittentFaultStrategy FailEveryN(int n, IFaultStrategy wrappedStrategy)
        {
            if (n <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "N must be positive");
            }

            var pattern = new bool[n];
            pattern[n - 1] = true; // Fail on every Nth operation
            
            return new IntermittentFaultStrategy(pattern, wrappedStrategy);
        }

        /// <inheritdoc/>
        public override string Name => $"Intermittent-{_wrappedStrategy.Name}";

        /// <inheritdoc/>
        public override string Description =>
            $"Injects {_wrappedStrategy.Name} fault based on pattern (length {_pattern.Length})";

        /// <inheritdoc/>
        public override FaultSeverity Severity => _wrappedStrategy.Severity;

        /// <inheritdoc/>
        public override async Task ExecuteAsync(FaultContext context, CancellationToken cancellationToken = default)
        {
            ValidateContext(context);

            bool shouldInject;
            int currentPosition;

            // Thread-safe position increment and pattern check
            lock (_lock)
            {
                currentPosition = _position;
                shouldInject = _pattern[_position];
                _position = (_position + 1) % _pattern.Length;
            }

            // Store pattern info in context metadata for observability
            context.Metadata["IntermittentPattern"] = string.Join(",", _pattern);
            context.Metadata["IntermittentPosition"] = currentPosition;
            context.Metadata["IntermittentShouldInject"] = shouldInject;

            if (shouldInject)
            {
                // Execute the wrapped fault strategy
                await _wrappedStrategy.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
            // If not injecting, simply return (no fault)
        }

        /// <summary>
        /// Resets the pattern position to the beginning.
        /// Useful for testing scenarios that need to restart the pattern.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _position = 0;
            }
        }
    }
}
