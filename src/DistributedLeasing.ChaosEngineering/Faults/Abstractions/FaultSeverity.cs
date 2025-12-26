namespace DistributedLeasing.ChaosEngineering.Faults.Abstractions
{
    /// <summary>
    /// Defines the severity level of a fault injection.
    /// Used for categorizing and prioritizing fault types.
    /// </summary>
    public enum FaultSeverity
    {
        /// <summary>
        /// Low severity - minimal impact on system behavior (e.g., small delays).
        /// </summary>
        Low = 0,

        /// <summary>
        /// Medium severity - moderate impact on system behavior (e.g., longer delays, transient failures).
        /// </summary>
        Medium = 1,

        /// <summary>
        /// High severity - significant impact on system behavior (e.g., operation failures, exceptions).
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical severity - severe impact that may cause cascading failures.
        /// </summary>
        Critical = 3
    }
}
