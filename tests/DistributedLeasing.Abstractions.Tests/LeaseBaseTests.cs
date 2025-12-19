using DistributedLeasing.Abstractions;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Exceptions;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="LeaseBase"/> abstract class.
/// </summary>
public class LeaseBaseTests
{
    private class TestLease : LeaseBase
    {
        public bool RenewCalled { get; private set; }
        public bool ReleaseCalled { get; set; }
        public bool ShouldThrowOnRenew { get; set; }
        public bool ShouldThrowOnRelease { get; set; }

        public TestLease(string leaseId, string leaseName, DateTimeOffset acquiredAt, TimeSpan duration)
            : base(leaseId, leaseName, acquiredAt, duration)
        {
        }

        protected override Task RenewLeaseAsync(CancellationToken cancellationToken)
        {
            RenewCalled = true;
            if (ShouldThrowOnRenew)
            {
                throw new LeaseRenewalException("Failed to renew lease")
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }
            return Task.CompletedTask;
        }

        protected override Task ReleaseLeaseAsync(CancellationToken cancellationToken)
        {
            ReleaseCalled = true;
            if (ShouldThrowOnRelease)
            {
                throw new InvalidOperationException("Release failed");
            }
            return Task.CompletedTask;
        }

        public void ExposeUpdateExpiration(DateTimeOffset newExpiration)
        {
            ExpiresAt = newExpiration;
        }
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesProperties()
    {
        // Arrange
        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(60);

        // Act
        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration);

        // Assert
        lease.LeaseId.Should().Be(leaseId);
        lease.LeaseName.Should().Be(leaseName);
        lease.AcquiredAt.Should().Be(acquiredAt);
        lease.ExpiresAt.Should().Be(acquiredAt.Add(duration));
        lease.IsAcquired.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidLeaseId_ThrowsArgumentException(string? invalidLeaseId)
    {
        // Arrange
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(60);

        // Act
        Action act = () => new TestLease(invalidLeaseId!, leaseName, acquiredAt, duration);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("leaseId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidLeaseName_ThrowsArgumentException(string? invalidLeaseName)
    {
        // Arrange
        var leaseId = Guid.NewGuid().ToString();
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(60);

        // Act
        Action act = () => new TestLease(leaseId, invalidLeaseName!, acquiredAt, duration);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("leaseName");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public void Constructor_WithInvalidDuration_ThrowsArgumentOutOfRangeException(int seconds)
    {
        // Arrange
        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(seconds);

        // Act
        Action act = () => new TestLease(leaseId, leaseName, acquiredAt, duration);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("duration");
    }

    [Fact]
    public void IsAcquired_WhenNotExpired_ReturnsTrue()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));

        // Act & Assert
        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task IsAcquired_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow.AddSeconds(-2),
            TimeSpan.FromSeconds(1));

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        // Act & Assert
        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task IsAcquired_WhenDisposed_ReturnsFalse()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));

        // Act
        await lease.DisposeAsync();

        // Assert
        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task RenewAsync_WhenNotDisposed_CallsRenewLeaseAsync()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60));

        // Act
        await lease.RenewAsync();

        // Assert
        lease.RenewCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RenewAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60));

        await lease.DisposeAsync();

        // Act
        Func<Task> act = async () => await lease.RenewAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
        lease.RenewCalled.Should().BeFalse();
    }

    [Fact]
    public async Task RenewAsync_WhenRenewFails_ThrowsLeaseRenewalException()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60))
        {
            ShouldThrowOnRenew = true
        };

        // Act
        Func<Task> act = async () => await lease.RenewAsync();

        // Assert
        await act.Should().ThrowAsync<LeaseRenewalException>()
            .WithMessage("*failed to renew*");
    }

    [Fact]
    public async Task ReleaseAsync_WhenNotDisposed_CallsReleaseLeaseAsync()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60));

        // Act
        await lease.ReleaseAsync();

        // Assert
        lease.ReleaseCalled.Should().BeTrue();
        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseAsync_WhenAlreadyReleased_DoesNotThrow()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60));

        await lease.ReleaseAsync();

        // Act
        Func<Task> act = async () => await lease.ReleaseAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReleaseAsync_WhenReleaseFails_DoesNotThrow()
    {
        // Arrange - Release should be idempotent and not throw
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60))
        {
            ShouldThrowOnRelease = true
        };

        // Act
        Func<Task> act = async () => await lease.ReleaseAsync();

        // Assert - Should swallow the exception for idempotency
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CallsReleaseAsync()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60));

        // Act
        await lease.DisposeAsync();

        // Assert
        lease.ReleaseCalled.Should().BeTrue();
        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_OnlyReleasesOnce()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60));

        // Act
        await lease.DisposeAsync();
        lease.ReleaseCalled = false; // Reset flag
        await lease.DisposeAsync();

        // Assert
        lease.ReleaseCalled.Should().BeFalse(); // Should not call release again
    }

    [Fact]
    public void UpdateExpiration_UpdatesExpiresAt()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(60));

        var newExpiration = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        lease.ExposeUpdateExpiration(newExpiration);

        // Assert
        lease.ExpiresAt.Should().Be(newExpiration);
    }

    [Fact]
    public async Task LeaseBase_ThreadSafety_MultipleThreadsAccessingIsAcquired()
    {
        // Arrange
        var lease = new TestLease(
            Guid.NewGuid().ToString(),
            "test-lease",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));

        var tasks = new List<Task<bool>>();

        // Act - Multiple threads reading IsAcquired
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => lease.IsAcquired));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should return true consistently
        results.Should().AllBeEquivalentTo(true);
    }
}
