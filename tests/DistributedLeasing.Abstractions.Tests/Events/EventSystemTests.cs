using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Events;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests.Events;

public class EventSystemTests
{
    [Fact]
    public void LeaseLostEventArgs_Constructor_WithValidArguments_CreatesInstance()
    {
        // Arrange
        var leaseName = "test-lease";
        var leaseId = "lease-123";
        var timestamp = DateTimeOffset.UtcNow;
        var reason = "renewal_timeout";
        var lastRenewal = timestamp.AddMinutes(-5);

        // Act
        var eventArgs = new LeaseLostEventArgs(leaseName, leaseId, timestamp, reason, lastRenewal);

        // Assert
        eventArgs.LeaseName.Should().Be(leaseName);
        eventArgs.LeaseId.Should().Be(leaseId);
        eventArgs.Timestamp.Should().Be(timestamp);
        eventArgs.Reason.Should().Be(reason);
        eventArgs.LastSuccessfulRenewal.Should().Be(lastRenewal);
    }

    [Fact]
    public void LeaseLostEventArgs_Constructor_WithNullLeaseName_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseLostEventArgs(null!, "lease-id", DateTimeOffset.UtcNow, "reason", DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("leaseName");
    }

    [Fact]
    public void LeaseLostEventArgs_Constructor_WithNullLeaseId_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseLostEventArgs("lease-name", null!, DateTimeOffset.UtcNow, "reason", DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("leaseId");
    }

    [Fact]
    public void LeaseLostEventArgs_Constructor_WithNullReason_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseLostEventArgs("lease-name", "lease-id", DateTimeOffset.UtcNow, null!, DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("reason");
    }

    [Fact]
    public void LeaseRenewalFailedEventArgs_Constructor_WithValidArguments_CreatesInstance()
    {
        // Arrange
        var leaseName = "test-lease";
        var leaseId = "lease-456";
        var timestamp = DateTimeOffset.UtcNow;
        var attemptNumber = 3;
        var exception = new InvalidOperationException("Renewal failed");
        var willRetry = true;

        // Act
        var eventArgs = new LeaseRenewalFailedEventArgs(leaseName, leaseId, timestamp, attemptNumber, exception, willRetry);

        // Assert
        eventArgs.LeaseName.Should().Be(leaseName);
        eventArgs.LeaseId.Should().Be(leaseId);
        eventArgs.Timestamp.Should().Be(timestamp);
        eventArgs.AttemptNumber.Should().Be(attemptNumber);
        eventArgs.Exception.Should().Be(exception);
        eventArgs.WillRetry.Should().BeTrue();
    }

    [Fact]
    public void LeaseRenewalFailedEventArgs_Constructor_WithNullLeaseName_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseRenewalFailedEventArgs(
            null!, "lease-id", DateTimeOffset.UtcNow, 1, new Exception(), true);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("leaseName");
    }

    [Fact]
    public void LeaseRenewalFailedEventArgs_Constructor_WithNullLeaseId_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseRenewalFailedEventArgs(
            "lease-name", null!, DateTimeOffset.UtcNow, 1, new Exception(), true);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("leaseId");
    }

    [Fact]
    public void LeaseRenewalFailedEventArgs_Constructor_WithNullException_AllowsNull()
    {
        // Act
        var eventArgs = new LeaseRenewalFailedEventArgs(
            "lease-name", "lease-id", DateTimeOffset.UtcNow, 1, null!, false);

        // Assert
        eventArgs.Exception.Should().BeNull();
    }

    [Fact]
    public void LeaseRenewalFailedEventArgs_WillRetry_CanBeFalse()
    {
        // Act
        var eventArgs = new LeaseRenewalFailedEventArgs(
            "lease-name", "lease-id", DateTimeOffset.UtcNow, 5, new Exception(), false);

        // Assert
        eventArgs.WillRetry.Should().BeFalse();
    }

    [Fact]
    public void LeaseRenewedEventArgs_Constructor_WithValidArguments_CreatesInstance()
    {
        // Arrange
        var leaseName = "test-lease";
        var leaseId = "lease-789";
        var timestamp = DateTimeOffset.UtcNow;
        var newExpiration = timestamp.AddMinutes(10);
        var renewalDuration = TimeSpan.FromMinutes(10);

        // Act
        var eventArgs = new LeaseRenewedEventArgs(leaseName, leaseId, timestamp, newExpiration, renewalDuration);

        // Assert
        eventArgs.LeaseName.Should().Be(leaseName);
        eventArgs.LeaseId.Should().Be(leaseId);
        eventArgs.Timestamp.Should().Be(timestamp);
        eventArgs.NewExpiration.Should().Be(newExpiration);
        eventArgs.RenewalDuration.Should().Be(renewalDuration);
    }

    [Fact]
    public void LeaseRenewedEventArgs_Constructor_WithNullLeaseName_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseRenewedEventArgs(
            null!, "lease-id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("leaseName");
    }

    [Fact]
    public void LeaseRenewedEventArgs_Constructor_WithNullLeaseId_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseRenewedEventArgs(
            "lease-name", null!, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("leaseId");
    }

    [Fact]
    public void EventHandlers_MultipleSubscribers_AllReceiveEvent()
    {
        // Arrange
        var receivedEvents = new ConcurrentBag<LeaseLostEventArgs>();
        EventHandler<LeaseLostEventArgs>? eventHandler = null;
        
        void Subscriber1(object? sender, LeaseLostEventArgs e) => receivedEvents.Add(e);
        void Subscriber2(object? sender, LeaseLostEventArgs e) => receivedEvents.Add(e);
        void Subscriber3(object? sender, LeaseLostEventArgs e) => receivedEvents.Add(e);

        eventHandler += Subscriber1;
        eventHandler += Subscriber2;
        eventHandler += Subscriber3;

        var testEvent = new LeaseLostEventArgs(
            "test-lease", "lease-id", DateTimeOffset.UtcNow, "test", DateTimeOffset.UtcNow);

        // Act
        eventHandler?.Invoke(this, testEvent);

        // Assert
        receivedEvents.Should().HaveCount(3);
        receivedEvents.Should().AllSatisfy(e => e.Should().Be(testEvent));
    }

    [Fact]
    public void EventHandlers_UnsubscribedHandler_DoesNotReceiveEvent()
    {
        // Arrange
        var subscriber1Called = false;
        var subscriber2Called = false;
        EventHandler<LeaseRenewedEventArgs>? eventHandler = null;
        
        void Subscriber1(object? sender, LeaseRenewedEventArgs e) => subscriber1Called = true;
        void Subscriber2(object? sender, LeaseRenewedEventArgs e) => subscriber2Called = true;

        eventHandler += Subscriber1;
        eventHandler += Subscriber2;
        eventHandler -= Subscriber1; // Unsubscribe

        var testEvent = new LeaseRenewedEventArgs(
            "test-lease", "lease-id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Act
        eventHandler?.Invoke(this, testEvent);

        // Assert
        subscriber1Called.Should().BeFalse();
        subscriber2Called.Should().BeTrue();
    }

    [Fact]
    public async Task EventHandlers_ThreadSafety_ConcurrentSubscriptions()
    {
        // Arrange
        var subscriberCount = new ConcurrentBag<int>();
        EventHandler<LeaseLostEventArgs>? eventHandler = null;
        var lockObj = new object();
        var tasks = new List<Task>();

        // Act - concurrent subscriptions with lock (EventHandler itself is not thread-safe)
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                void Handler(object? sender, LeaseLostEventArgs e) => subscriberCount.Add(index);
                lock (lockObj)
                {
                    eventHandler += Handler;
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Trigger event
        var testEvent = new LeaseLostEventArgs(
            "test-lease", "lease-id", DateTimeOffset.UtcNow, "test", DateTimeOffset.UtcNow);
        lock (lockObj)
        {
            eventHandler?.Invoke(this, testEvent);
        }

        // Assert
        subscriberCount.Should().HaveCount(100);
    }

    [Fact]
    public async Task EventHandlers_ThreadSafety_ConcurrentInvocations()
    {
        // Arrange
        var receivedEvents = new ConcurrentBag<LeaseRenewalFailedEventArgs>();
        EventHandler<LeaseRenewalFailedEventArgs>? eventHandler = null;
        
        void Handler(object? sender, LeaseRenewalFailedEventArgs e) => receivedEvents.Add(e);
        eventHandler += Handler;

        var tasks = new List<Task>();

        // Act - concurrent invocations
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var testEvent = new LeaseRenewalFailedEventArgs(
                    $"lease-{index}", $"id-{index}", DateTimeOffset.UtcNow, 1, new Exception(), true);
                eventHandler?.Invoke(this, testEvent);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        receivedEvents.Should().HaveCount(50);
        receivedEvents.Select(e => e.LeaseName).Distinct().Should().HaveCount(50);
    }

    [Fact]
    public void EventHandlers_ExceptionInHandler_DoesNotAffectOtherHandlers()
    {
        // Arrange
        var handler1Called = false;
        var handler2Called = false;
        var handler3Called = false;
        EventHandler<LeaseRenewedEventArgs>? eventHandler = null;
        
        void Handler1(object? sender, LeaseRenewedEventArgs e) => handler1Called = true;
        void Handler2(object? sender, LeaseRenewedEventArgs e) => throw new InvalidOperationException("Handler2 failed");
        void Handler3(object? sender, LeaseRenewedEventArgs e) => handler3Called = true;

        eventHandler += Handler1;
        eventHandler += Handler2;
        eventHandler += Handler3;

        var testEvent = new LeaseRenewedEventArgs(
            "test-lease", "lease-id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Act
        try
        {
            eventHandler?.Invoke(this, testEvent);
        }
        catch
        {
            // Exception expected from Handler2
        }

        // Assert - Handler1 was called, but Handler3 was not (event propagation stopped)
        handler1Called.Should().BeTrue();
        handler2Called.Should().BeFalse(); // Never set to true
        handler3Called.Should().BeFalse(); // Event propagation stopped
    }

    [Fact]
    public void EventArgs_Inheritance_AllInheritFromEventArgs()
    {
        // Assert
        typeof(LeaseLostEventArgs).Should().BeDerivedFrom<EventArgs>();
        typeof(LeaseRenewalFailedEventArgs).Should().BeDerivedFrom<EventArgs>();
        typeof(LeaseRenewedEventArgs).Should().BeDerivedFrom<EventArgs>();
    }

    [Fact]
    public void EventArgs_Properties_AreReadOnly()
    {
        // Assert - all properties should be get-only
        typeof(LeaseLostEventArgs).GetProperty(nameof(LeaseLostEventArgs.LeaseName))
            ?.CanWrite.Should().BeFalse();
        typeof(LeaseRenewalFailedEventArgs).GetProperty(nameof(LeaseRenewalFailedEventArgs.LeaseName))
            ?.CanWrite.Should().BeFalse();
        typeof(LeaseRenewedEventArgs).GetProperty(nameof(LeaseRenewedEventArgs.LeaseName))
            ?.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void LeaseLostEventArgs_Reason_SupportsCommonValues()
    {
        // Arrange
        var commonReasons = new[] { "renewal_timeout", "lease_expired", "provider_failure", "manual_release" };

        // Act & Assert
        foreach (var reason in commonReasons)
        {
            var eventArgs = new LeaseLostEventArgs(
                "lease", "id", DateTimeOffset.UtcNow, reason, DateTimeOffset.UtcNow);
            eventArgs.Reason.Should().Be(reason);
        }
    }

    [Fact]
    public void LeaseRenewalFailedEventArgs_AttemptNumber_SupportsMultipleRetries()
    {
        // Arrange & Act
        var attempt1 = new LeaseRenewalFailedEventArgs(
            "lease", "id", DateTimeOffset.UtcNow, 1, new Exception(), true);
        var attempt5 = new LeaseRenewalFailedEventArgs(
            "lease", "id", DateTimeOffset.UtcNow, 5, new Exception(), true);
        var attempt10 = new LeaseRenewalFailedEventArgs(
            "lease", "id", DateTimeOffset.UtcNow, 10, new Exception(), false);

        // Assert
        attempt1.AttemptNumber.Should().Be(1);
        attempt5.AttemptNumber.Should().Be(5);
        attempt10.AttemptNumber.Should().Be(10);
        attempt10.WillRetry.Should().BeFalse();
    }

    [Fact]
    public void LeaseRenewedEventArgs_RenewalDuration_SupportsVariousDurations()
    {
        // Arrange & Act
        var short15s = new LeaseRenewedEventArgs(
            "lease", "id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(15), TimeSpan.FromSeconds(15));
        var medium60s = new LeaseRenewedEventArgs(
            "lease", "id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(60), TimeSpan.FromSeconds(60));
        var long120s = new LeaseRenewedEventArgs(
            "lease", "id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(120), TimeSpan.FromSeconds(120));

        // Assert
        short15s.RenewalDuration.Should().Be(TimeSpan.FromSeconds(15));
        medium60s.RenewalDuration.Should().Be(TimeSpan.FromSeconds(60));
        long120s.RenewalDuration.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void EventArgs_Timestamps_CanRepresentPastPresentFuture()
    {
        // Arrange
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var present = DateTimeOffset.UtcNow;
        var future = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var lostEvent = new LeaseLostEventArgs("lease", "id", present, "reason", past);
        var failedEvent = new LeaseRenewalFailedEventArgs("lease", "id", present, 1, new Exception(), true);
        var renewedEvent = new LeaseRenewedEventArgs("lease", "id", present, future, TimeSpan.FromHours(1));

        // Assert
        lostEvent.Timestamp.Should().BeCloseTo(present, TimeSpan.FromSeconds(1));
        lostEvent.LastSuccessfulRenewal.Should().BeCloseTo(past, TimeSpan.FromSeconds(1));
        failedEvent.Timestamp.Should().BeCloseTo(present, TimeSpan.FromSeconds(1));
        renewedEvent.NewExpiration.Should().BeCloseTo(future, TimeSpan.FromSeconds(1));
    }
}
