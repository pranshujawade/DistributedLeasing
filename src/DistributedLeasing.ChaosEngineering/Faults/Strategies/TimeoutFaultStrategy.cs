using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Faults.Strategies
{
    /// <summary>
    /// Fault strategy that simulates operation timeout by cancelling the operation after a delay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy was defined in the original ChaosFaultType enum but never implemented.
    /// It simulates timeout scenarios by throwing <see cref="OperationCanceledException"/>
    /// after a configurable duration.
    /// </para>
    /// <para>
    /// This is different from <see cref="DelayFaultStrategy"/> which simply adds latency.
    /// TimeoutFaultStrategy actively cancels the operation to simulate timeout behavior.
    /// </para>
    /// </remarks>
    public class TimeoutFaultStrategy : FaultStrategyBase
    {
        private readonly TimeSpan _timeoutDuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutFaultStrategy"/> class.
        /// </summary>
        /// <param name="timeoutDuration">The duration after which to simulate a timeout.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when timeoutDuration is zero or negative.
        /// </exception>
        public TimeoutFaultStrategy(TimeSpan timeoutDuration)
        {
            if (timeoutDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutDuration),
                    "Timeout duration must be positive");
            }

            _timeoutDuration = timeoutDuration;
        }

        /// <inheritdoc/>
        public override string Name => "Timeout";

        /// <inheritdoc/>
        public override string Description =>
            $"Simulates operation timeout after {_timeoutDuration.TotalMilliseconds}ms";

        /// <inheritdoc/>
        public override FaultSeverity Severity
        {
            get
            {
                // Categorize based on timeout duration
                if (_timeoutDuration.TotalSeconds < 1)
                    return FaultSeverity.Medium;
                if (_timeoutDuration.TotalSeconds < 5)
                    return FaultSeverity.High;
                return FaultSeverity.Critical;
            }
        }

        /// <inheritdoc/>
        public override async Task ExecuteAsync(FaultContext context, CancellationToken cancellationToken = default)
        {
            ValidateContext(context);

            // Store timeout info in context metadata for observability
            context.Metadata["TimeoutDuration"] = _timeoutDuration;

            // Create a new CancellationTokenSource that will be cancelled after the timeout duration
            using var timeoutCts = new CancellationTokenSource(_timeoutDuration);
            
            // Link with the provided cancellation token so either can cancel
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeoutCts.Token);

            try
            {
                // Wait until timeout or external cancellation
                await Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout occurred - throw with appropriate message
                throw new OperationCanceledException(
                    $"Operation timed out after {_timeoutDuration.TotalMilliseconds}ms (chaos timeout fault injection)");
            }
            // If external cancellation occurred, let the exception propagate naturally
        }
    }
}
