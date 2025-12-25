using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Observability;
using DistributedLeasing.Tests.Shared.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests.Observability;

#if NET5_0_OR_GREATER
public class LeaseHealthCheckTests : IClassFixture<LoggingFixture>
{
    private readonly LoggingFixture _loggingFixture;

    public LeaseHealthCheckTests(LoggingFixture loggingFixture)
    {
        _loggingFixture = loggingFixture;
    }

    [Fact]
    public void Constructor_NullProvider_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new LeaseHealthCheck(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("provider");
    }

    [Fact]
    public void Constructor_WithDefaults_SetsDefaultValues()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();

        // Act
        var healthCheck = new LeaseHealthCheck(mockProvider.Object);

        // Assert
        healthCheck.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomValues_AcceptsAllParameters()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();
        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var customLeaseName = "custom-health-check";
        var customTimeout = TimeSpan.FromSeconds(10);

        // Act
        var healthCheck = new LeaseHealthCheck(
            mockProvider.Object,
            logger,
            customLeaseName,
            customTimeout);

        // Assert
        healthCheck.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_SuccessfulAcquisitionAndRelease_ReturnsHealthy()
    {
        // Arrange
        var mockLease = new Mock<ILease>();
        mockLease.Setup(l => l.LeaseId).Returns("test-lease-id");
        mockLease.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(mockProvider.Object, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Lease provider is healthy");
        result.Data.Should().ContainKey("provider");
        result.Data.Should().ContainKey("lease_name");
        result.Data.Should().ContainKey("lease_id");
        result.Data["lease_id"].Should().Be("test-lease-id");
        result.Data["status"].Should().Be("acquired_and_released");

        mockLease.Verify(l => l.ReleaseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_LeaseAlreadyHeld_ReturnsHealthy()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILease?)null); // Lease already held

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(mockProvider.Object, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Lease provider is responsive (lease currently held)");
        result.Data.Should().ContainKey("provider");
        result.Data.Should().ContainKey("lease_name");
        result.Data["status"].Should().Be("held");
    }

    [Fact]
    public async Task CheckHealthAsync_AcquisitionSucceedsReleaseThrows_ReturnsDegraded()
    {
        // Arrange
        var mockLease = new Mock<ILease>();
        mockLease.Setup(l => l.LeaseId).Returns("test-lease-id");
        mockLease.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Release failed"));

        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(mockProvider.Object, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Lease provider acquisition succeeded but release failed");
        result.Exception.Should().BeOfType<InvalidOperationException>();
        result.Data.Should().ContainKey("provider");
        result.Data.Should().ContainKey("lease_id");
        result.Data["lease_id"].Should().Be("test-lease-id");
        result.Data["status"].Should().Be("acquired_release_failed");
    }

    [Fact]
    public async Task CheckHealthAsync_ProviderThrowsException_ReturnsUnhealthy()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Provider unavailable");
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(mockProvider.Object, logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Lease provider is unhealthy");
        result.Exception.Should().Be(expectedException);
        result.Data.Should().ContainKey("provider");
        result.Data.Should().ContainKey("exception");
        result.Data["exception"].Should().Be("InvalidOperationException");
        result.Data["error"].Should().Be("Provider unavailable");
    }

    [Fact]
    public async Task CheckHealthAsync_TimeoutOccurs_ReturnsDegraded()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ILease?>();
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string name, TimeSpan duration, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct); // Longer than timeout
                return await tcs.Task;
            });

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(
            mockProvider.Object,
            logger,
            timeout: TimeSpan.FromMilliseconds(100)); // Short timeout
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("timed out");
        result.Data.Should().ContainKey("timeout_ms");
        result.Data["timeout_ms"].Should().Be(100.0);
    }

    [Fact]
    public async Task CheckHealthAsync_CancellationRequested_ReturnsDegraded()
    {
        // Arrange
        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string name, TimeSpan duration, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return null;
            });

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(mockProvider.Object, logger);
        var context = new HealthCheckContext();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Act
        var result = await healthCheck.CheckHealthAsync(context, cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("timed out");
    }

    [Fact]
    public async Task CheckHealthAsync_WithCustomLeaseName_UsesCustomName()
    {
        // Arrange
        var customLeaseName = "custom-health-lease";
        string? capturedLeaseName = null;

        var mockLease = new Mock<ILease>();
        mockLease.Setup(l => l.LeaseId).Returns("test-id");
        mockLease.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, CancellationToken>((name, duration, ct) => 
                capturedLeaseName = name)
            .ReturnsAsync(mockLease.Object);

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(
            mockProvider.Object,
            logger,
            healthCheckLeaseName: customLeaseName);
        var context = new HealthCheckContext();

        // Act
        await healthCheck.CheckHealthAsync(context);

        // Assert
        capturedLeaseName.Should().Be(customLeaseName);
    }

    [Fact]
    public async Task CheckHealthAsync_WithCustomTimeout_UsesCustomTimeout()
    {
        // Arrange
        var customTimeout = TimeSpan.FromSeconds(3);
        var mockLease = new Mock<ILease>();
        mockLease.Setup(l => l.LeaseId).Returns("test-id");
        mockLease.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(
            mockProvider.Object,
            logger,
            timeout: customTimeout);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_DisposesLeaseOnSuccess()
    {
        // Arrange
        var mockLease = new Mock<ILease>();
        mockLease.Setup(l => l.LeaseId).Returns("test-lease-id");
        mockLease.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var logger = _loggingFixture.CreateLogger<LeaseHealthCheck>();
        var healthCheck = new LeaseHealthCheck(mockProvider.Object, logger);
        var context = new HealthCheckContext();

        // Act
        await healthCheck.CheckHealthAsync(context);

        // Assert - using statement should have disposed the lease
        // We verify this indirectly by checking the lease was released
        mockLease.Verify(l => l.ReleaseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_ProviderTypeName_IncludedInHealthData()
    {
        // Arrange
        var mockLease = new Mock<ILease>();
        mockLease.Setup(l => l.LeaseId).Returns("test-id");
        mockLease.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockProvider = new Mock<ILeaseProvider>();
        mockProvider.Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLease.Object);

        var healthCheck = new LeaseHealthCheck(mockProvider.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("provider");
        result.Data["provider"].ToString().Should().Contain("ILeaseProvider");
    }
}
#endif
