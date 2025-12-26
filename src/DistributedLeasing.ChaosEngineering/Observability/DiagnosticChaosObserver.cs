using System;
using System.Diagnostics;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Observability
{
    /// <summary>
    /// Observer that writes chaos events to System.Diagnostics trace listeners.
    /// Useful for integration with existing diagnostic infrastructure.
    /// </summary>
    public class DiagnosticChaosObserver : IChaosObserver
    {
        private readonly string _sourceName;
        private readonly TraceSwitch _traceSwitch;

        /// <summary>
        /// Initializes a new instance of the DiagnosticChaosObserver class.
        /// </summary>
        /// <param name="sourceName">The source name for diagnostic events (default: "ChaosEngineering").</param>
        /// <param name="traceSwitch">Optional trace switch for controlling output level.</param>
        public DiagnosticChaosObserver(string sourceName = "ChaosEngineering", TraceSwitch? traceSwitch = null)
        {
            _sourceName = sourceName ?? "ChaosEngineering";
            _traceSwitch = traceSwitch ?? new TraceSwitch("ChaosEngineering", "Chaos Engineering Events");
        }

        /// <inheritdoc/>
        public void OnFaultDecisionMade(FaultContext context, FaultDecision decision, string policyName)
        {
            if (_traceSwitch.TraceInfo)
            {
                var action = decision.ShouldInjectFault ? "INJECT" : "SKIP";
                Trace.TraceInformation(
                    $"[{_sourceName}] Decision: {action} by '{policyName}' for {context.Operation} on '{context.LeaseName}' - {decision.Reason}");
            }
        }

        /// <inheritdoc/>
        public void OnFaultExecuting(FaultContext context, IFaultStrategy strategy)
        {
            if (_traceSwitch.TraceVerbose)
            {
                Trace.TraceInformation(
                    $"[{_sourceName}] Executing fault '{strategy.Name}' (Severity: {strategy.Severity}) for {context.Operation} on '{context.LeaseName}'");
            }
        }

        /// <inheritdoc/>
        public void OnFaultExecuted(FaultContext context, IFaultStrategy strategy, TimeSpan duration)
        {
            if (_traceSwitch.TraceInfo)
            {
                Trace.TraceInformation(
                    $"[{_sourceName}] Executed fault '{strategy.Name}' in {duration.TotalMilliseconds:F2}ms for {context.Operation} on '{context.LeaseName}'");
            }
        }

        /// <inheritdoc/>
        public void OnFaultExecutionFailed(FaultContext context, IFaultStrategy strategy, Exception exception)
        {
            if (_traceSwitch.TraceError)
            {
                Trace.TraceError(
                    $"[{_sourceName}] Fault execution failed: '{strategy.Name}' for {context.Operation} on '{context.LeaseName}' - {exception.GetType().Name}: {exception.Message}");
            }
        }

        /// <inheritdoc/>
        public void OnFaultSkipped(FaultContext context, string reason)
        {
            if (_traceSwitch.TraceVerbose)
            {
                Trace.TraceInformation(
                    $"[{_sourceName}] Fault skipped for {context.Operation} on '{context.LeaseName}': {reason}");
            }
        }
    }
}
