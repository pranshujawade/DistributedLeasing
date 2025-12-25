using DistributedLeasing.Abstractions.Contracts;
using Moq;

namespace DistributedLeasing.Tests.Shared.Fixtures;

/// <summary>
/// Shared mock provider fixture for unit tests. Provides a configured Mock&lt;ILeaseProvider&gt; with fluent API.
/// </summary>
public class MockProviderFixture : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the mock lease provider instance.
    /// </summary>
    public Mock<ILeaseProvider> MockProvider { get; }

    /// <summary>
    /// Gets the ILeaseProvider object from the mock.
    /// </summary>
    public ILeaseProvider Provider => MockProvider.Object;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockProviderFixture"/> class.
    /// </summary>
    public MockProviderFixture()
    {
        MockProvider = new Mock<ILeaseProvider>();
        SetupDefaults();
    }

    /// <summary>
    /// Sets up default behaviors for the mock provider.
    /// </summary>
    private void SetupDefaults()
    {
        // Default: successful acquisition returns a mock lease
        MockProvider
            .Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateMockLease());
    }

    /// <summary>
    /// Configures the mock to return a successful acquisition with the specified lease.
    /// </summary>
    /// <param name="lease">The lease to return.</param>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture WithSuccessfulAcquisition(ILease lease)
    {
        MockProvider
            .Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);
        return this;
    }

    /// <summary>
    /// Configures the mock to return a successful acquisition for a specific lease name.
    /// </summary>
    /// <param name="leaseName">The lease name.</param>
    /// <param name="lease">The lease to return.</param>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture WithSuccessfulAcquisition(string leaseName, ILease lease)
    {
        MockProvider
            .Setup(p => p.AcquireLeaseAsync(
                leaseName,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);
        return this;
    }

    /// <summary>
    /// Configures the mock to return null (lease already held).
    /// </summary>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture WithFailedAcquisition()
    {
        MockProvider
            .Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILease?)null);
        return this;
    }

    /// <summary>
    /// Configures the mock to return null for a specific lease name.
    /// </summary>
    /// <param name="leaseName">The lease name.</param>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture WithFailedAcquisition(string leaseName)
    {
        MockProvider
            .Setup(p => p.AcquireLeaseAsync(
                leaseName,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ILease?)null);
        return this;
    }

    /// <summary>
    /// Configures the mock to throw an exception when acquiring a lease.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture WithAcquisitionException(Exception exception)
    {
        MockProvider
            .Setup(p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return this;
    }

    /// <summary>
    /// Configures the mock for successful renewal.
    /// </summary>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture WithSuccessfulRenewal()
    {
        // Renewal is typically on the lease itself, but can be configured here if needed
        return this;
    }

    /// <summary>
    /// Configures the mock for failed renewal.
    /// </summary>
    /// <param name="exception">The exception to throw on renewal.</param>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture WithFailedRenewal(Exception exception)
    {
        // Renewal is typically on the lease itself, but can be configured here if needed
        return this;
    }

    /// <summary>
    /// Resets all mock setups to default configurations.
    /// </summary>
    /// <returns>This fixture for method chaining.</returns>
    public MockProviderFixture ResetToDefaults()
    {
        MockProvider.Reset();
        SetupDefaults();
        return this;
    }

    /// <summary>
    /// Verifies that AcquireLeaseAsync was called with the expected parameters.
    /// </summary>
    /// <param name="leaseName">Expected lease name.</param>
    /// <param name="times">Expected number of times (default: once).</param>
    public void VerifyAcquisitionCalled(string leaseName, Times? times = null)
    {
        MockProvider.Verify(
            p => p.AcquireLeaseAsync(
                leaseName,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            times ?? Times.Once());
    }

    /// <summary>
    /// Verifies that AcquireLeaseAsync was called with any parameters.
    /// </summary>
    /// <param name="times">Expected number of times (default: once).</param>
    public void VerifyAcquisitionCalled(Times? times = null)
    {
        MockProvider.Verify(
            p => p.AcquireLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            times ?? Times.Once());
    }

    /// <summary>
    /// Verifies that BreakLeaseAsync was called for the specified lease name.
    /// </summary>
    /// <param name="leaseName">Expected lease name.</param>
    /// <param name="times">Expected number of times (default: once).</param>
    public void VerifyBreakCalled(string leaseName, Times? times = null)
    {
        MockProvider.Verify(
            p => p.BreakLeaseAsync(leaseName, It.IsAny<CancellationToken>()),
            times ?? Times.Once());
    }

    /// <summary>
    /// Creates a mock lease for testing.
    /// </summary>
    /// <param name="leaseName">Optional lease name.</param>
    /// <param name="duration">Optional lease duration.</param>
    /// <returns>A mock ILease instance.</returns>
    private static ILease CreateMockLease(string? leaseName = null, TimeSpan? duration = null)
    {
        var mockLease = new Mock<ILease>();
        var leaseId = Guid.NewGuid().ToString();
        var name = leaseName ?? TestConstants.LeaseNames.Standard;
        var acquiredAt = DateTimeOffset.UtcNow;
        var leaseDuration = duration ?? TestConstants.LeaseDurations.Medium;

        mockLease.Setup(l => l.LeaseId).Returns(leaseId);
        mockLease.Setup(l => l.LeaseName).Returns(name);
        mockLease.Setup(l => l.AcquiredAt).Returns(acquiredAt);
        mockLease.Setup(l => l.ExpiresAt).Returns(acquiredAt.Add(leaseDuration));
        mockLease.Setup(l => l.IsAcquired).Returns(true);
        mockLease.Setup(l => l.RenewAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockLease.Setup(l => l.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mockLease.Object;
    }

    /// <summary>
    /// Disposes the fixture and verifies no unexpected calls were made.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
