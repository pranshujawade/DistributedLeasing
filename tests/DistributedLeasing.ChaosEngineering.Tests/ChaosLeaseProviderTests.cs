using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Exceptions;
using DistributedLeasing.ChaosEngineering;
using FluentAssertions;
using Moq;
using Xunit;

namespace DistributedLeasing.ChaosEngineering.Tests;

/// <summary>
/// Tests for the legacy ChaosLeaseProvider (v4.x API).
/// </summary>
public class ChaosLeaseProviderTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesProvider()
    {
        // Arrange
        var innerProvider = new Mock<ILeaseProvider>().Object;
        var policy = new ChaosPolicy();

        // Act
        var chaosProvider = new ChaosLeaseProvider(innerProvider, policy);

        // Assert
        chaosProvider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullInnerProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var policy = new ChaosPolicy();

        // Act
        var act = () => new ChaosLeaseProvider(null!, policy);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("innerProvider");
    }

    [Fact]
    public void Constructor_WithNullPolicy_ThrowsArgumentNullException()
    {
        // Arrange
        var innerProvider = new Mock<ILeaseProvider>().Object;

        // Act
        var act = () => new ChaosLeaseProvider(innerProvider, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("policy");
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithNoFaults_CallsInnerProvider()
    {
        // Arrange
        var mockLease = new Mock<ILease>();
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider
            .Setup(p => p.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var policy = new ChaosPolicy { FaultTypes = ChaosFaultType.None };
        var chaosProvider = new ChaosLeaseProvider(mockProvider.Object, policy);

        // Act
        var lease = await chaosProvider.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(30));

        // Assert
        lease.Should().Be(mockLease.Object);
        mockProvider.Verify(
            p => p.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithDelayFault_IntroducesDelay()
    {
        // Arrange
        var mockLease = new Mock<ILease>();
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider
            .Setup(p => p.AcquireLeaseAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var policy = new ChaosPolicy
        {
            FailureRate = 1.0, // Always inject fault
            FaultTypes = ChaosFaultType.Delay,
            MinDelay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromMilliseconds(200)
        };
        var chaosProvider = new ChaosLeaseProvider(mockProvider.Object, policy);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await chaosProvider.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(30));
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(190); // Allow for timing variance
        mockProvider.Verify(
            p => p.AcquireLeaseAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithExceptionFault_ThrowsProviderUnavailableException()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();
        var policy = new ChaosPolicy
        {
            FailureRate = 1.0, // Always inject fault
            FaultTypes = ChaosFaultType.Exception
        };
        var chaosProvider = new ChaosLeaseProvider(mockProvider.Object, policy);

        // Act
        var act = async () => await chaosProvider.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(30));

        // Assert
        await act.Should().ThrowAsync<ProviderUnavailableException>()
            .WithMessage("*Chaos fault injection*");
    }

    [Fact]
    public async Task BreakLeaseAsync_WithNoFaults_CallsInnerProvider()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider
            .Setup(p => p.BreakLeaseAsync("test-lease", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var policy = new ChaosPolicy { FaultTypes = ChaosFaultType.None };
        var chaosProvider = new ChaosLeaseProvider(mockProvider.Object, policy);

        // Act
        await chaosProvider.BreakLeaseAsync("test-lease");

        // Assert
        mockProvider.Verify(
            p => p.BreakLeaseAsync("test-lease", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BreakLeaseAsync_WithDelayFault_IntroducesDelay()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider
            .Setup(p => p.BreakLeaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var policy = new ChaosPolicy
        {
            FailureRate = 1.0, // Always inject fault
            FaultTypes = ChaosFaultType.Delay,
            MinDelay = TimeSpan.FromMilliseconds(150),
            MaxDelay = TimeSpan.FromMilliseconds(150)
        };
        var chaosProvider = new ChaosLeaseProvider(mockProvider.Object, policy);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await chaosProvider.BreakLeaseAsync("test-lease");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(140);
        mockProvider.Verify(
            p => p.BreakLeaseAsync("test-lease", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BreakLeaseAsync_WithExceptionFault_ThrowsProviderUnavailableException()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();
        var policy = new ChaosPolicy
        {
            FailureRate = 1.0, // Always inject fault
            FaultTypes = ChaosFaultType.Exception
        };
        var chaosProvider = new ChaosLeaseProvider(mockProvider.Object, policy);

        // Act
        var act = async () => await chaosProvider.BreakLeaseAsync("test-lease");

        // Assert
        await act.Should().ThrowAsync<ProviderUnavailableException>()
            .WithMessage("*Chaos fault injection*");
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithLowFailureRate_SometimesSucceeds()
    {
        // Arrange
        var mockLease = new Mock<ILease>();
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider
            .Setup(p => p.AcquireLeaseAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var policy = new ChaosPolicy
        {
            FailureRate = 0.3, // 30% failure rate
            FaultTypes = ChaosFaultType.Exception
        };
        var chaosProvider = new ChaosLeaseProvider(mockProvider.Object, policy);

        // Act - Run multiple attempts
        int successCount = 0;
        int exceptionCount = 0;

        for (int i = 0; i < 100; i++)
        {
            try
            {
                await chaosProvider.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(30));
                successCount++;
            }
            catch (ProviderUnavailableException)
            {
                exceptionCount++;
            }
        }

        // Assert - Should have both successes and failures
        successCount.Should().BeGreaterThan(0, "some requests should succeed");
        exceptionCount.Should().BeGreaterThan(0, "some requests should fail");
        
        // Rough statistical validation (30% ± 15% tolerance)
        exceptionCount.Should().BeInRange(15, 45);
    }

    [Fact]
    public void ChaosPolicy_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var policy = new ChaosPolicy();

        // Assert
        policy.FailureRate.Should().Be(0.1);
        policy.MinDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(2));
        policy.FaultTypes.Should().Be(ChaosFaultType.All);
    }

    [Fact]
    public void ChaosFaultType_Flags_CanBeCombined()
    {
        // Arrange & Act
        var combined = ChaosFaultType.Delay | ChaosFaultType.Exception;

        // Assert
        combined.Should().HaveFlag(ChaosFaultType.Delay);
        combined.Should().HaveFlag(ChaosFaultType.Exception);
        combined.Should().NotHaveFlag(ChaosFaultType.Timeout);
    }
}
