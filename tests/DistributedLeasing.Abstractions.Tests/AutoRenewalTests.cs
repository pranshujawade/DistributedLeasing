using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Configuration;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Core;
using DistributedLeasing.Abstractions.Events;
using DistributedLeasing.Abstractions.Exceptions;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests;

/// <summary>
/// Unit tests for auto-renewal functionality in <see cref="LeaseBase"/>.
/// </summary>
public class AutoRenewalTests
{
    private class TestLease : LeaseBase
    {
        public bool RenewCalled { get; private set; }
        public bool ReleaseCalled { get; private set; }
        public int RenewCallCount { get; private set; }
        public bool ShouldThrowOnRenew { get; set; }
        public bool ShouldThrowOnRelease { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public TestLease(string leaseId, string leaseName, DateTimeOffset acquiredAt, TimeSpan duration, LeaseOptions? options = null)
            : base(leaseId, leaseName, acquiredAt, duration, options)
        {
        }

        protected override Task RenewLeaseAsync(CancellationToken cancellationToken)
        {
            RenewCallCount++;
            RenewCalled = true;
            
            if (ShouldThrowOnRenew)
            {
                if (ExceptionToThrow != null)
                    throw ExceptionToThrow;
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
                if (ExceptionToThrow != null)
                    throw ExceptionToThrow;
                throw new InvalidOperationException("Release failed");
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Constructor_WithAutoRenewEnabled_StartsAutoRenewal()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(2),
            AutoRenewInterval = TimeSpan.FromSeconds(1)
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(2);

        // Act
        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options);

        // Give some time for auto-renewal to potentially happen
        await Task.Delay(TimeSpan.FromMilliseconds(1100));

        // Assert
        lease.RenewCallCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task Constructor_WithAutoRenewDisabled_DoesNotStartAutoRenewal()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = false,
            DefaultLeaseDuration = TimeSpan.FromSeconds(2),
            AutoRenewInterval = TimeSpan.FromSeconds(1)
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(2);

        // Act
        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options);

        // Give some time for auto-renewal to potentially happen
        await Task.Delay(TimeSpan.FromMilliseconds(1100));

        // Assert
        lease.RenewCalled.Should().BeFalse();
    }

    [Fact]
    public async Task LeaseRenewed_Event_IsRaised_OnSuccessfulRenewal()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(2),
            AutoRenewInterval = TimeSpan.FromMilliseconds(500)
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(2);

        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options);
        var eventRaised = false;
        LeaseRenewedEventArgs? eventArgs = null;

        lease.LeaseRenewed += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act - Wait for auto-renewal
        await Task.Delay(TimeSpan.FromMilliseconds(600));

        // Assert
        eventRaised.Should().BeTrue();
        eventArgs.Should().NotBeNull();
        eventArgs!.LeaseName.Should().Be(leaseName);
        eventArgs.LeaseId.Should().Be(leaseId);
        eventArgs.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task LeaseRenewalFailed_Event_IsRaised_OnFailedRenewal()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(2),
            AutoRenewInterval = TimeSpan.FromMilliseconds(500),
            AutoRenewMaxRetries = 0 // No retries to make test faster
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(2);

        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options)
        {
            ShouldThrowOnRenew = true
        };

        var eventRaised = false;
        LeaseRenewalFailedEventArgs? eventArgs = null;

        lease.LeaseRenewalFailed += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act - Wait for auto-renewal to fail
        await Task.Delay(TimeSpan.FromMilliseconds(2000));

        // Assert
        eventRaised.Should().BeTrue();
        eventArgs.Should().NotBeNull();
        eventArgs!.LeaseName.Should().Be(leaseName);
        eventArgs.LeaseId.Should().Be(leaseId);
        eventArgs.AttemptNumber.Should().Be(1);
        eventArgs.WillRetry.Should().BeFalse(); // No retries configured
    }

    [Fact]
    public async Task LeaseLost_Event_IsRaised_WhenLeaseIsLost()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(2),
            AutoRenewInterval = TimeSpan.FromMilliseconds(500),
            AutoRenewMaxRetries = 0 // No retries to make test faster
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(2);

        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options)
        {
            ShouldThrowOnRenew = true
        };

        var eventRaised = false;
        LeaseLostEventArgs? eventArgs = null;

        lease.LeaseLost += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act - Wait for auto-renewal to fail and lease to be lost
        await Task.Delay(TimeSpan.FromMilliseconds(2000));

        // Assert
        eventRaised.Should().BeTrue();
        eventArgs.Should().NotBeNull();
        eventArgs!.LeaseName.Should().Be(leaseName);
        eventArgs.LeaseId.Should().Be(leaseId);
        eventArgs.Reason.Should().Contain("failed");
        lease.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public async Task RenewalCount_Increments_OnSuccessfulRenewal()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(2),
            AutoRenewInterval = TimeSpan.FromMilliseconds(500)
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(2);

        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options);
        var initialRenewalCount = lease.RenewalCount;

        // Act - Wait for auto-renewal
        await Task.Delay(TimeSpan.FromMilliseconds(600));

        // Assert
        lease.RenewalCount.Should().BeGreaterThan(initialRenewalCount);
    }

    [Fact]
    public async Task DisposeAsync_StopsAutoRenewal()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(5),
            AutoRenewInterval = TimeSpan.FromMilliseconds(200)
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(5);

        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options);
        var initialRenewalCount = lease.RenewalCount;

        // Act - Dispose the lease
        await lease.DisposeAsync();

        // Wait to ensure no more renewals happen
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        // Assert
        lease.IsAcquired.Should().BeFalse();
        lease.RenewalCount.Should().Be(initialRenewalCount); // No additional renewals
    }

    [Fact]
    public async Task ReleaseAsync_StopsAutoRenewal()
    {
        // Arrange
        var options = new LeaseOptions
        {
            AutoRenew = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(5),
            AutoRenewInterval = TimeSpan.FromMilliseconds(200)
        };

        var leaseId = Guid.NewGuid().ToString();
        var leaseName = "test-lease";
        var acquiredAt = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromSeconds(5);

        var lease = new TestLease(leaseId, leaseName, acquiredAt, duration, options);
        var initialRenewalCount = lease.RenewalCount;

        // Act - Release the lease
        await lease.ReleaseAsync();

        // Wait to ensure no more renewals happen
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        // Assert
        lease.IsAcquired.Should().BeFalse();
        lease.RenewalCount.Should().Be(initialRenewalCount); // No additional renewals
    }
}