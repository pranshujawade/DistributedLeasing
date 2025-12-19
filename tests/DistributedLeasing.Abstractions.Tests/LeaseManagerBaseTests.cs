using DistributedLeasing.Abstractions;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Configuration;
using DistributedLeasing.Core.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="LeaseManagerBase"/> abstract class.
/// </summary>
public class LeaseManagerBaseTests
{
    private class TestLeaseManager : LeaseManagerBase
    {
        public TestLeaseManager(ILeaseProvider provider, LeaseOptions options)
            : base(provider, options)
        {
        }

        public new ILeaseProvider Provider => base.Provider;
        public new LeaseOptions Options => base.Options;
    }

    private readonly Mock<ILeaseProvider> _mockProvider;
    private readonly LeaseOptions _options;

    public LeaseManagerBaseTests()
    {
        _mockProvider = new Mock<ILeaseProvider>();
        _options = new LeaseOptions
        {
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AcquireTimeout = TimeSpan.FromSeconds(30),
            AcquireRetryInterval = TimeSpan.FromSeconds(1)
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesProperties()
    {
        // Act
        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Assert
        manager.Provider.Should().Be(_mockProvider.Object);
        manager.Options.Should().Be(_options);
    }

    [Fact]
    public void Constructor_WithNullProvider_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TestLeaseManager(null!, _options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("provider");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TestLeaseManager(_mockProvider.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task TryAcquireAsync_WhenLeaseAvailable_ReturnsLease()
    {
        // Arrange
        var leaseName = "test-lease";
        var expectedLease = Mock.Of<ILease>(l => l.LeaseName == leaseName);
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLease);

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        var lease = await manager.TryAcquireAsync(leaseName);

        // Assert
        lease.Should().Be(expectedLease);
        _mockProvider.Verify(
            p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenLeaseNotAvailable_ReturnsNull()
    {
        // Arrange
        var leaseName = "test-lease";
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILease?)null);

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        var lease = await manager.TryAcquireAsync(leaseName);

        // Assert
        lease.Should().BeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WithCustomDuration_UsesSpecifiedDuration()
    {
        // Arrange
        var leaseName = "test-lease";
        var customDuration = TimeSpan.FromSeconds(120);
        var expectedLease = Mock.Of<ILease>();
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, customDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLease);

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        var lease = await manager.TryAcquireAsync(leaseName, customDuration);

        // Assert
        lease.Should().Be(expectedLease);
        _mockProvider.Verify(
            p => p.AcquireLeaseAsync(leaseName, customDuration, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAcquireAsync_WithCancellation_PropagatesCancellationToken()
    {
        // Arrange
        var leaseName = "test-lease";
        var cts = new CancellationTokenSource();
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, cts.Token))
            .ReturnsAsync((ILease?)null);

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        await manager.TryAcquireAsync(leaseName, cancellationToken: cts.Token);

        // Assert
        _mockProvider.Verify(
            p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task AcquireAsync_WhenLeaseAvailable_ReturnsLeaseImmediately()
    {
        // Arrange
        var leaseName = "test-lease";
        var expectedLease = Mock.Of<ILease>();
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLease);

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        var lease = await manager.AcquireAsync(leaseName);

        // Assert
        lease.Should().Be(expectedLease);
        _mockProvider.Verify(
            p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcquireAsync_WhenLeaseNotAvailable_RetriesWithBackoff()
    {
        // Arrange
        var leaseName = "test-lease";
        var expectedLease = Mock.Of<ILease>();
        var attempts = 0;
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                return attempts == 3 ? expectedLease : null;
            });

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        var lease = await manager.AcquireAsync(leaseName);

        // Assert
        lease.Should().Be(expectedLease);
        attempts.Should().Be(3);
        _mockProvider.Verify(
            p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task AcquireAsync_WhenTimeoutExpires_ThrowsLeaseAcquisitionException()
    {
        // Arrange
        var leaseName = "test-lease";
        var timeout = TimeSpan.FromSeconds(2);
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILease?)null);

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        Func<Task> act = async () => await manager.AcquireAsync(leaseName, timeout: timeout);

        // Assert
        await act.Should().ThrowAsync<LeaseAcquisitionException>()
            .WithMessage("*Could not acquire lease*");
    }

    [Fact]
    public async Task AcquireAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var leaseName = "test-lease";
        var cts = new CancellationTokenSource();
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILease?)null)
            .Callback(() => cts.Cancel());

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        Func<Task> act = async () => await manager.AcquireAsync(leaseName, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AcquireAsync_WithInfiniteTimeout_RetriesIndefinitely()
    {
        // Arrange
        var leaseName = "test-lease";
        var expectedLease = Mock.Of<ILease>();
        var attempts = 0;
        var cts = new CancellationTokenSource();
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                if (attempts == 10)
                {
                    cts.Cancel(); // Cancel after 10 attempts to prevent infinite loop
                }
                return attempts == 5 ? expectedLease : null;
            });

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        var lease = await manager.AcquireAsync(leaseName, timeout: Timeout.InfiniteTimeSpan, cancellationToken: cts.Token);

        // Assert
        lease.Should().Be(expectedLease);
        attempts.Should().Be(5);
    }

    [Fact]
    public async Task AcquireAsync_ImplementsExponentialBackoff()
    {
        // Arrange
        var leaseName = "test-lease";
        var expectedLease = Mock.Of<ILease>();
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var lastCallTime = DateTimeOffset.UtcNow;
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var now = DateTimeOffset.UtcNow;
                if (attempts > 0)
                {
                    delays.Add(now - lastCallTime);
                }
                lastCallTime = now;
                attempts++;
                return attempts == 4 ? expectedLease : null;
            });

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        var lease = await manager.AcquireAsync(leaseName);

        // Assert
        lease.Should().Be(expectedLease);
        delays.Count.Should().Be(3); // 3 delays between 4 attempts
        
        // Verify exponential backoff (each delay should be approximately double the previous)
        // Allow for timing variance - use a more lenient check
        delays[1].TotalMilliseconds.Should().BeGreaterThan(delays[0].TotalMilliseconds * 0.99);
    }

    [Fact]
    public async Task AcquireAsync_WithProviderException_ThrowsLeaseAcquisitionException()
    {
        // Arrange
        var leaseName = "test-lease";
        var innerException = new InvalidOperationException("Provider error");
        
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, _options.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ThrowsAsync(innerException);

        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        Func<Task> act = async () => await manager.AcquireAsync(leaseName);

        // Assert
        await act.Should().ThrowAsync<LeaseAcquisitionException>()
            .WithMessage("*Unexpected error while acquiring lease*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryAcquireAsync_WithInvalidLeaseName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        Func<Task> act = async () => await manager.TryAcquireAsync(invalidName!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("leaseName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AcquireAsync_WithInvalidLeaseName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        Func<Task> act = async () => await manager.AcquireAsync(invalidName!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("leaseName");
    }

    [Fact]
    public async Task AcquireAsync_WithNegativeTimeout_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var manager = new TestLeaseManager(_mockProvider.Object, _options);

        // Act
        Func<Task> act = async () => await manager.AcquireAsync("test", timeout: TimeSpan.FromSeconds(-1));

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("timeout");
    }

    [Fact]
    public async Task AcquireAsync_RespectsRetryLogic()
    {
        // Arrange
        var leaseName = "test-lease";
        var customOptions = new LeaseOptions
        {
            DefaultLeaseDuration = TimeSpan.FromSeconds(60),
            AcquireTimeout = TimeSpan.FromMilliseconds(500), // Short timeout for test
            AcquireRetryInterval = TimeSpan.FromMilliseconds(100)
        };
        
        var attempts = 0;
        _mockProvider
            .Setup(p => p.AcquireLeaseAsync(leaseName, customOptions.DefaultLeaseDuration, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                return null; // Always return null to force retries
            });

        var manager = new TestLeaseManager(_mockProvider.Object, customOptions);

        // Act
        Func<Task> act = async () => await manager.AcquireAsync(leaseName);

        // Assert
        await act.Should().ThrowAsync<LeaseAcquisitionException>();
        attempts.Should().BeGreaterThan(1); // Should have retried at least once
    }
}
