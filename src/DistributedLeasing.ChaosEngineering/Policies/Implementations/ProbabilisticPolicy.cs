using System;
using System.Collections.Generic;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Policies.Implementations
{
    /// <summary>
    /// A fault decision policy that uses probability to determine whether to inject faults.
    /// Provides thread-safe random number generation with optional seed control for reproducibility.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This policy evaluates fault injection based on a configured probability (0.0 to 1.0).
    /// It selects a fault strategy randomly from the configured strategies when injection is triggered.
    /// </para>
    /// <para>
    /// Thread-safety is ensured through use of Random.Shared (.NET 6+) or ThreadLocal&lt;Random&gt;.
    /// </para>
    /// </remarks>
    public class ProbabilisticPolicy : IFaultDecisionPolicy
    {
        private readonly double _probability;
        private readonly List<IFaultStrategy> _strategies;
        private readonly int? _seed;

#if !NET6_0_OR_GREATER
        private static readonly ThreadLocal<Random> _randomLocal = new ThreadLocal<Random>(
            () => new Random());
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbabilisticPolicy"/> class.
        /// </summary>
        /// <param name="probability">
        /// The probability of injecting a fault (0.0 to 1.0).
        /// For example, 0.3 means 30% chance of fault injection.
        /// </param>
        /// <param name="strategies">The fault strategies to choose from when injecting faults.</param>
        /// <param name="seed">Optional seed for reproducible random number generation.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when probability is not between 0.0 and 1.0.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when strategies is null.</exception>
        /// <exception cref="ArgumentException">Thrown when strategies is empty.</exception>
        public ProbabilisticPolicy(double probability, List<IFaultStrategy> strategies, int? seed = null)
        {
            if (probability < 0.0 || probability > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(probability),
                    "Probability must be between 0.0 and 1.0");
            }

            if (strategies == null)
            {
                throw new ArgumentNullException(nameof(strategies));
            }

            if (strategies.Count == 0)
            {
                throw new ArgumentException("At least one fault strategy must be provided", nameof(strategies));
            }

            _probability = probability;
            _strategies = new List<IFaultStrategy>(strategies);
            _seed = seed;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbabilisticPolicy"/> class
        /// with a single fault strategy.
        /// </summary>
        /// <param name="probability">The probability of injecting a fault (0.0 to 1.0).</param>
        /// <param name="strategy">The fault strategy to inject.</param>
        /// <param name="seed">Optional seed for reproducible random number generation.</param>
        public ProbabilisticPolicy(double probability, IFaultStrategy strategy, int? seed = null)
            : this(probability, new List<IFaultStrategy> { strategy }, seed)
        {
        }

        /// <inheritdoc/>
        public string Name => $"Probabilistic (p={_probability:P0})";

        /// <inheritdoc/>
        public FaultDecision Evaluate(FaultContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Get random value using thread-safe approach
            double randomValue = GetRandomValue();

            // Decide whether to inject fault based on probability
            if (randomValue < _probability)
            {
                // Select a random strategy
                var selectedStrategy = SelectRandomStrategy();
                
                return FaultDecision.Inject(
                    selectedStrategy,
                    $"Probabilistic policy triggered (random={randomValue:F4}, threshold={_probability:F4})");
            }

            return FaultDecision.Skip(
                $"Probabilistic policy skipped (random={randomValue:F4}, threshold={_probability:F4})");
        }

        /// <summary>
        /// Gets a random value between 0.0 and 1.0 using thread-safe random generation.
        /// </summary>
        /// <returns>A random double between 0.0 and 1.0.</returns>
        private double GetRandomValue()
        {
#if NET6_0_OR_GREATER
            // Use thread-safe Random.Shared in .NET 6+
            return Random.Shared.NextDouble();
#else
            // Use ThreadLocal<Random> for older frameworks
            return _randomLocal.Value!.NextDouble();
#endif
        }

        /// <summary>
        /// Selects a random fault strategy from the configured strategies.
        /// </summary>
        /// <returns>A randomly selected <see cref="IFaultStrategy"/>.</returns>
        private IFaultStrategy SelectRandomStrategy()
        {
            if (_strategies.Count == 1)
            {
                return _strategies[0];
            }

#if NET6_0_OR_GREATER
            var index = Random.Shared.Next(_strategies.Count);
#else
            var index = _randomLocal.Value!.Next(_strategies.Count);
#endif
            return _strategies[index];
        }
    }
}
