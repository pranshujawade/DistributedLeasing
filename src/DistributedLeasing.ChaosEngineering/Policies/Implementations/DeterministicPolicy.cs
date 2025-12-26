using System;
using System.Collections.Generic;
using System.Threading;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Policies.Implementations
{
    /// <summary>
    /// A fault decision policy that uses a deterministic sequence to decide when to inject faults.
    /// Provides reproducible, predictable fault injection for testing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy cycles through a predefined sequence of decisions, making it ideal for:
    /// - Reproducible test scenarios
    /// - CI/CD pipeline testing
    /// - Debugging specific failure sequences
    /// - Validating retry logic with known patterns
    /// </para>
    /// <para>
    /// Thread-safe through synchronized position tracking.
    /// </para>
    /// </remarks>
    public class DeterministicPolicy : IFaultDecisionPolicy
    {
        private readonly List<bool> _sequence;
        private readonly IFaultStrategy _strategy;
        private int _position;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="DeterministicPolicy"/> class.
        /// </summary>
        /// <param name="sequence">
        /// A boolean sequence where <c>true</c> means inject fault, <c>false</c> means skip.
        /// The sequence repeats indefinitely.
        /// </param>
        /// <param name="strategy">The fault strategy to inject when sequence indicates true.</param>
        /// <exception cref="ArgumentNullException">Thrown when sequence or strategy is null.</exception>
        /// <exception cref="ArgumentException">Thrown when sequence is empty.</exception>
        public DeterministicPolicy(List<bool> sequence, IFaultStrategy strategy)
        {
            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            if (sequence.Count == 0)
            {
                throw new ArgumentException("Sequence cannot be empty", nameof(sequence));
            }

            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            _sequence = new List<bool>(sequence);
            _strategy = strategy;
            _position = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeterministicPolicy"/> class
        /// from a boolean array.
        /// </summary>
        /// <param name="sequence">A boolean array sequence.</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        public DeterministicPolicy(bool[] sequence, IFaultStrategy strategy)
            : this(new List<bool>(sequence ?? throw new ArgumentNullException(nameof(sequence))), strategy)
        {
        }

        /// <summary>
        /// Creates a deterministic policy that injects faults for the first N operations.
        /// </summary>
        /// <param name="failCount">Number of operations to fail before succeeding.</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <returns>A <see cref="DeterministicPolicy"/> configured to fail N times.</returns>
        public static DeterministicPolicy FailFirstN(int failCount, IFaultStrategy strategy)
        {
            if (failCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(failCount), "Fail count must be positive");
            }

            var sequence = new List<bool>();
            for (int i = 0; i < failCount; i++)
            {
                sequence.Add(true);
            }
            sequence.Add(false); // Succeed after failures

            return new DeterministicPolicy(sequence, strategy);
        }

        /// <summary>
        /// Creates a deterministic policy that fails every Nth operation.
        /// </summary>
        /// <param name="n">Fail every Nth operation (e.g., 2 = every other operation fails).</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <returns>A <see cref="DeterministicPolicy"/> configured to fail periodically.</returns>
        public static DeterministicPolicy FailEveryN(int n, IFaultStrategy strategy)
        {
            if (n <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "N must be greater than 1");
            }

            var sequence = new List<bool>();
            for (int i = 0; i < n - 1; i++)
            {
                sequence.Add(false);
            }
            sequence.Add(true); // Fail on every Nth

            return new DeterministicPolicy(sequence, strategy);
        }

        /// <summary>
        /// Creates a deterministic policy that alternates between success and failure.
        /// </summary>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <returns>A <see cref="DeterministicPolicy"/> that alternates.</returns>
        public static DeterministicPolicy Alternate(IFaultStrategy strategy)
        {
            return new DeterministicPolicy(new[] { false, true }, strategy);
        }

        /// <inheritdoc/>
        public string Name => $"Deterministic (sequence length={_sequence.Count})";

        /// <inheritdoc/>
        public FaultDecision Evaluate(FaultContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            bool shouldInject;
            int currentPosition;

            // Thread-safe position increment and sequence check
            lock (_lock)
            {
                currentPosition = _position;
                shouldInject = _sequence[_position];
                _position = (_position + 1) % _sequence.Count;
            }

            // Store sequence info in decision metadata
            var metadata = new Dictionary<string, object>
            {
                ["SequencePosition"] = currentPosition,
                ["SequenceLength"] = _sequence.Count,
                ["NextPosition"] = _position
            };

            if (shouldInject)
            {
                return new FaultDecision
                {
                    ShouldInjectFault = true,
                    FaultStrategy = _strategy,
                    Reason = $"Deterministic policy triggered at position {currentPosition}/{_sequence.Count}",
                    Metadata = metadata
                };
            }

            return new FaultDecision
            {
                ShouldInjectFault = false,
                Reason = $"Deterministic policy skipped at position {currentPosition}/{_sequence.Count}",
                Metadata = metadata
            };
        }

        /// <summary>
        /// Resets the sequence position to the beginning.
        /// Useful for restarting test scenarios.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _position = 0;
            }
        }

        /// <summary>
        /// Gets the current position in the sequence.
        /// </summary>
        public int CurrentPosition
        {
            get
            {
                lock (_lock)
                {
                    return _position;
                }
            }
        }
    }
}
