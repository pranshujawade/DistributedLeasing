using DistributedLeasing.Abstractions;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Configuration;
using DistributedLeasing.Core.Events;
using DistributedLeasing.Core.Exceptions;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests;

public class DebugAutoRenewalTest
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
    public async Task Debug_AutoRenewal_FailedRenewal()
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
            Console.WriteLine($"LeaseRenewalFailed event received at {DateTimeOffset.UtcNow}");
            eventRaised = true;
            eventArgs = args;
        };

        // Act - Wait for auto-renewal to fail
        Console.WriteLine($"Waiting for auto-renewal to fail at {DateTimeOffset.UtcNow}");
        await Task.Delay(TimeSpan.FromMilliseconds(2000));

        // Assert
        Console.WriteLine($"eventRaised: {eventRaised}");
        eventRaised.Should().BeTrue();
        eventArgs.Should().NotBeNull();
        eventArgs!.LeaseName.Should().Be(leaseName);
        eventArgs.LeaseId.Should().Be(leaseId);
        eventArgs.AttemptNumber.Should().Be(1);
        eventArgs.WillRetry.Should().BeFalse(); // No retries configured
    }
}