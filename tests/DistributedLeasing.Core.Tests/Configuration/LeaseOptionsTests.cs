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
        options.AutoRenewInterval.Should().Be(TimeSpan.FromSeconds(30));
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
        options.AutoRenewInterval.Should().Be(TimeSpan.FromSeconds(60));
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
    public void Validate_WhenAutoRenewIntervalIsLessThanDuration_DoesNotThrow()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AutoRenewInterval = TimeSpan.FromSeconds(30)
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
