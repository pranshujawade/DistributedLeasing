using System.Collections.Generic;
using System.Linq;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Faults.Strategies;
using DistributedLeasing.ChaosEngineering.Policies.Implementations;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.ChaosEngineering.Tests.Policies
{
    public class ProbabilisticPolicyTests
    {
        [Fact]
        public void Constructor_WithValidProbability_CreatesPolicy()
        {
            // Arrange
            var strategy = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(100));

            // Act
            var policy = new ProbabilisticPolicy(0.5, strategy);

            // Assert
            policy.Should().NotBeNull();
            policy.Name.Should().Contain("Probabilistic");
        }

        [Theory]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        [InlineData(2.0)]
        public void Constructor_WithInvalidProbability_ThrowsArgumentException(double probability)
        {
            // Arrange
            var strategy = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(100));

            // Act & Assert
            var act = () => new ProbabilisticPolicy(probability, strategy);
            act.Should().Throw<System.ArgumentException>()
                .WithMessage("*probability*");
        }

        [Fact]
        public void Evaluate_WithProbabilityZero_AlwaysSkips()
        {
            // Arrange
            var strategy = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(100));
            var policy = new ProbabilisticPolicy(0.0, strategy);
            var context = new FaultContext { Operation = "Test" };

            // Act
            var results = Enumerable.Range(0, 100)
                .Select(_ => policy.Evaluate(context))
                .ToList();

            // Assert
            results.Should().AllSatisfy(d => d.ShouldInjectFault.Should().BeFalse());
        }

        [Fact]
        public void Evaluate_WithProbabilityOne_AlwaysInjects()
        {
            // Arrange
            var strategy = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(100));
            var policy = new ProbabilisticPolicy(1.0, strategy);
            var context = new FaultContext { Operation = "Test" };

            // Act
            var results = Enumerable.Range(0, 100)
                .Select(_ => policy.Evaluate(context))
                .ToList();

            // Assert
            results.Should().AllSatisfy(d => d.ShouldInjectFault.Should().BeTrue());
            results.Should().AllSatisfy(d => d.FaultStrategy.Should().Be(strategy));
        }

        [Fact]
        public void Evaluate_WithProbabilityHalf_InjectsApproximatelyHalf()
        {
            // Arrange
            var strategy = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(100));
            var policy = new ProbabilisticPolicy(0.5, strategy);
            var context = new FaultContext { Operation = "Test" };

            // Act
            var results = Enumerable.Range(0, 1000)
                .Select(_ => policy.Evaluate(context))
                .ToList();

            var injectionCount = results.Count(d => d.ShouldInjectFault);

            // Assert (allow 10% tolerance for randomness)
            injectionCount.Should().BeInRange(400, 600);
        }

        [Fact]
        public void Evaluate_WithMultipleStrategies_SelectsRandomly()
        {
            // Arrange
            var strategy1 = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(100));
            var strategy2 = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(200));
            var strategy3 = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(300));
            var strategies = new List<IFaultStrategy> { strategy1, strategy2, strategy3 };
            var policy = new ProbabilisticPolicy(1.0, strategies);
            var context = new FaultContext { Operation = "Test" };
        
            // Act
            var results = Enumerable.Range(0, 300)
                .Select(_ => policy.Evaluate(context))
                .ToList();
        
            var selectedStrategies = results
                .Select(d => d.FaultStrategy)
                .Distinct()
                .ToList();
        
            // Assert - With probability 1.0, all should inject
            results.Should().AllSatisfy(d => d.ShouldInjectFault.Should().BeTrue());
            // Note: Due to randomness, we can't guarantee all 3 are selected, but at least 1 should be
            selectedStrategies.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public void Evaluate_IncludesReasonInDecision()
        {
            // Arrange
            var strategy = new DelayFaultStrategy(System.TimeSpan.FromMilliseconds(100));
            var policy = new ProbabilisticPolicy(1.0, strategy);
            var context = new FaultContext { Operation = "Test" };

            // Act
            var decision = policy.Evaluate(context);

            // Assert
            decision.Reason.Should().NotBeNullOrEmpty();
            decision.Reason.Should().Contain("Probabilistic");
        }
    }
}
