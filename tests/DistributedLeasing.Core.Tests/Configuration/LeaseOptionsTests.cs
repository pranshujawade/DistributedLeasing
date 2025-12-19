using DistributedLeasing.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Core.Tests.Configuration;

/// <summary>
/// Unit tests for the LeaseOptions configuration class.
/// </summary>
public class LeaseOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var options = new LeaseOptions();

        // Assert
        options.DefaultLeaseDuration.Should().Be(TimeSpan.FromSeconds(60));
        options.AutoRenew.Should().BeFalse();
        options.AutoRenewInterval.Should().Be(TimeSpan.FromSeconds(40)); // Updated to 2/3 of default
        options.AutoRenewRetryInterval.Should().Be(TimeSpan.FromSeconds(5));
        options.AutoRenewMaxRetries.Should().Be(3);
        options.AutoRenewSafetyThreshold.Should().Be(0.9);
        options.AcquireTimeout.Should().Be(Timeout.InfiniteTimeSpan);
        options.AcquireRetryInterval.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void DefaultLeaseDuration_WhenSet_AutoAdjustsAutoRenewInterval()
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        options.DefaultLeaseDuration = TimeSpan.FromSeconds(120);

        // Assert
        options.AutoRenewInterval.Should().Be(TimeSpan.FromSeconds(80)); // 2/3 of 120 seconds
    }

    [Fact]
    public void DefaultLeaseDuration_WhenSetToInfinite_DoesNotAdjustAutoRenewInterval()
    {
        // Arrange
        var options = new LeaseOptions();
        var originalAutoRenewInterval = options.AutoRenewInterval;

        // Act
        options.DefaultLeaseDuration = Timeout.InfiniteTimeSpan;

        // Assert
        options.AutoRenewInterval.Should().Be(originalAutoRenewInterval);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void DefaultLeaseDuration_WhenSetToInvalidValue_ThrowsArgumentOutOfRangeException(int seconds)
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        Action act = () => options.DefaultLeaseDuration = TimeSpan.FromSeconds(seconds);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void AutoRenewInterval_WhenExplicitlySet_DoesNotAutoAdjust()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenewInterval = TimeSpan.FromSeconds(20)
        };

        // Act
        options.DefaultLeaseDuration = TimeSpan.FromSeconds(120);

        // Assert - should remain at 20, not change to 60
        options.AutoRenewInterval.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void AutoRenewInterval_WhenSetToInvalidValue_ThrowsArgumentOutOfRangeException(int seconds)
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        Action act = () => options.AutoRenewInterval = TimeSpan.FromSeconds(seconds);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void AutoRenewRetryInterval_WhenSetToInvalidValue_ThrowsArgumentOutOfRangeException(int seconds)
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        Action act = () => options.AutoRenewRetryInterval = TimeSpan.FromSeconds(seconds);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void AutoRenewMaxRetries_WhenSetToValidValue_SetsValue(int retries)
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        options.AutoRenewMaxRetries = retries;

        // Assert
        options.AutoRenewMaxRetries.Should().Be(retries);
    }

    [Fact]
    public void AutoRenewMaxRetries_WhenSetToNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        Action act = () => options.AutoRenewMaxRetries = -2;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(0.9)]
    [InlineData(0.95)]
    public void AutoRenewSafetyThreshold_WhenSetToValidValue_SetsValue(double threshold)
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        options.AutoRenewSafetyThreshold = threshold;

        // Assert
        options.AutoRenewSafetyThreshold.Should().Be(threshold);
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(0.49)]
    [InlineData(0.96)]
    [InlineData(1.0)]
    public void AutoRenewSafetyThreshold_WhenSetToInvalidValue_ThrowsArgumentOutOfRangeException(double threshold)
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        Action act = () => options.AutoRenewSafetyThreshold = threshold;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void AcquireTimeout_CanBeSetToInfinite()
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        options.AcquireTimeout = Timeout.InfiniteTimeSpan;

        // Assert
        options.AcquireTimeout.Should().Be(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void AcquireTimeout_WhenSetToNegativeNonInfinite_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        Action act = () => options.AcquireTimeout = TimeSpan.FromSeconds(-5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void AcquireRetryInterval_WhenSetToInvalidValue_ThrowsArgumentOutOfRangeException(int seconds)
    {
        // Arrange
        var options = new LeaseOptions();

        // Act
        Action act = () => options.AcquireRetryInterval = TimeSpan.FromSeconds(seconds);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void Validate_WhenAutoRenewIntervalIsTooCloseToDuration_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AutoRenewInterval = TimeSpan.FromSeconds(55) // Too close to 60s with default 0.9 threshold
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*should be less than 90%*");
    }

    [Fact]
    public void Validate_WhenAutoRenewRetryIntervalIsTooLarge_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AutoRenewInterval = TimeSpan.FromSeconds(30),
            AutoRenewRetryInterval = TimeSpan.FromSeconds(40) // Too large for 30s interval
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*too large for the buffer time*");
    }

    [Fact]
    public void Validate_WhenAutoRenewIntervalIsLessThanDurationButValid_DoesNotThrow()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AutoRenewInterval = TimeSpan.FromSeconds(30), // Less than duration and within safety threshold
            AutoRenewSafetyThreshold = 0.9
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenAutoRenewIntervalIsGreaterThanDuration_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AutoRenewInterval = TimeSpan.FromSeconds(70)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AutoRenewInterval*DefaultLeaseDuration*");
    }

    [Fact]
    public void Validate_WhenAutoRenewIsFalse_DoesNotValidateIntervals()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = false,
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AutoRenewInterval = TimeSpan.FromSeconds(70)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenDurationIsInfinite_DoesNotValidateAutoRenewInterval()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = Timeout.InfiniteTimeSpan,
            AutoRenewInterval = TimeSpan.FromSeconds(30)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }
}
