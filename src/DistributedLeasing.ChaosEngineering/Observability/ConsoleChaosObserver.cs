using System;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Observability
{
    /// <summary>
    /// Simple console-based observer for chaos events.
    /// Useful for development, debugging, and demonstrations.
    /// </summary>
    public class ConsoleChaosObserver : IChaosObserver
    {
        private readonly bool _useColors;
        private readonly bool _includeTimestamps;

        /// <summary>
        /// Initializes a new instance of the ConsoleChaosObserver class.
        /// </summary>
        /// <param name="useColors">Whether to use console colors (default: true).</param>
        /// <param name="includeTimestamps">Whether to include timestamps in output (default: true).</param>
        public ConsoleChaosObserver(bool useColors = true, bool includeTimestamps = true)
        {
            _useColors = useColors;
            _includeTimestamps = includeTimestamps;
        }

        /// <inheritdoc/>
        public void OnFaultDecisionMade(FaultContext context, FaultDecision decision, string policyName)
        {
            var color = decision.ShouldInjectFault ? ConsoleColor.Yellow : ConsoleColor.Gray;
            var action = decision.ShouldInjectFault ? "INJECT" : "SKIP";
            var strategyName = decision.FaultStrategy?.Name ?? "N/A";

            WriteColorLine(
                $"[{action}] Decision by '{policyName}' for {context.Operation} on '{context.LeaseName}': {decision.Reason} (Strategy: {strategyName})",
                color);
        }

        /// <inheritdoc/>
        public void OnFaultExecuting(FaultContext context, IFaultStrategy strategy)
        {
            WriteColorLine(
                $"[EXECUTING] Fault '{strategy.Name}' (Severity: {strategy.Severity}) for {context.Operation} on '{context.LeaseName}'",
                ConsoleColor.Magenta);
        }

        /// <inheritdoc/>
        public void OnFaultExecuted(FaultContext context, IFaultStrategy strategy, TimeSpan duration)
        {
            WriteColorLine(
                $"[EXECUTED] Fault '{strategy.Name}' completed in {duration.TotalMilliseconds:F2}ms for {context.Operation} on '{context.LeaseName}'",
                ConsoleColor.Green);
        }

        /// <inheritdoc/>
        public void OnFaultExecutionFailed(FaultContext context, IFaultStrategy strategy, Exception exception)
        {
            WriteColorLine(
                $"[FAILED] Fault '{strategy.Name}' execution failed for {context.Operation} on '{context.LeaseName}': {exception.GetType().Name} - {exception.Message}",
                ConsoleColor.Red);
        }

        /// <inheritdoc/>
        public void OnFaultSkipped(FaultContext context, string reason)
        {
            WriteColorLine(
                $"[SKIPPED] Fault skipped for {context.Operation} on '{context.LeaseName}': {reason}",
                ConsoleColor.DarkGray);
        }

        private void WriteColorLine(string message, ConsoleColor color)
        {
            var fullMessage = _includeTimestamps
                ? $"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}"
                : message;

            if (_useColors)
            {
                var originalColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine(fullMessage);
                }
                finally
                {
                    Console.ForegroundColor = originalColor;
                }
            }
            else
            {
                Console.WriteLine(fullMessage);
            }
        }
    }
}
