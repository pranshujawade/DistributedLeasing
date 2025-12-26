using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Faults.Strategies
{
    /// <summary>
    /// Fault strategy that injects artificial delay (latency) into operations.
    /// Uses thread-safe random generation for variable delays.
    /// </summary>
    /// <remarks>
    /// This strategy is useful for testing timeout handling, retry logic, and
    /// system behavior under slow network conditions.
    /// </remarks>
    public class DelayFaultStrategy : FaultStrategyBase
    {
        private readonly TimeSpan _minDelay;
        private readonly TimeSpan _maxDelay;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelayFaultStrategy"/> class.
        /// </summary>
        /// <param name="minDelay">The minimum delay duration.</param>
        /// <param name="maxDelay">The maximum delay duration.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when minDelay is negative, maxDelay is negative, or minDelay > maxDelay.
        /// </exception>
        public DelayFaultStrategy(TimeSpan minDelay, TimeSpan maxDelay)
        {
            if (minDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minDelay), 
                    "Minimum delay cannot be negative");
            }

            if (maxDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDelay), 
                    "Maximum delay cannot be negative");
            }

            if (minDelay > maxDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(minDelay), 
                    "Minimum delay cannot be greater than maximum delay");
            }

            _minDelay = minDelay;
            _maxDelay = maxDelay;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelayFaultStrategy"/> class with a fixed delay.
        /// </summary>
        /// <param name="delay">The fixed delay duration.</param>
        public DelayFaultStrategy(TimeSpan delay) 
            : this(delay, delay)
        {
        }

        /// <inheritdoc/>
        public override string Name => "Delay";

        /// <inheritdoc/>
        public override string Description => 
            _minDelay == _maxDelay 
                ? $"Injects a fixed delay of {_minDelay.TotalMilliseconds}ms"
                : $"Injects a random delay between {_minDelay.TotalMilliseconds}ms and {_maxDelay.TotalMilliseconds}ms";

        /// <inheritdoc/>
        public override FaultSeverity Severity
        {
            get
            {
                // Categorize based on maximum delay
                if (_maxDelay.TotalSeconds < 1)
                    return FaultSeverity.Low;
                if (_maxDelay.TotalSeconds < 5)
                    return FaultSeverity.Medium;
                if (_maxDelay.TotalSeconds < 30)
                    return FaultSeverity.High;
                return FaultSeverity.Critical;
            }
        }

        /// <inheritdoc/>
        public override async Task ExecuteAsync(FaultContext context, CancellationToken cancellationToken = default)
        {
            ValidateContext(context);

            TimeSpan delay;

            if (_minDelay == _maxDelay)
            {
                // Fixed delay
                delay = _minDelay;
            }
            else
            {
                // Random delay using thread-safe Random.Shared (.NET 6+)
                // For older frameworks, this would need ThreadLocal<Random>
#if NET6_0_OR_GREATER
                var randomMilliseconds = Random.Shared.Next(
                    (int)_minDelay.TotalMilliseconds,
                    (int)_maxDelay.TotalMilliseconds + 1);
#else
                // Fallback for older frameworks - use lock-based approach
                lock (typeof(DelayFaultStrategy))
                {
                    var random = new Random();
                    var randomMilliseconds = random.Next(
                        (int)_minDelay.TotalMilliseconds,
                        (int)_maxDelay.TotalMilliseconds + 1);
                    delay = TimeSpan.FromMilliseconds(randomMilliseconds);
                }
#endif

#if NET6_0_OR_GREATER
                delay = TimeSpan.FromMilliseconds(randomMilliseconds);
#endif
            }

            // Store the actual delay in context metadata for observability
            context.Metadata["InjectedDelay"] = delay;

            // Inject the delay, respecting cancellation
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
