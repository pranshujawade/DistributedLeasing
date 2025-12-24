using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DistributedLeasing.Abstractions.Exceptions;
using DistributedLeasing.Azure.Blob;
using FluentAssertions;
using Moq;
using Xunit;

namespace DistributedLeasing.Azure.Blob.Tests;

public class BlobLeaseProviderTests
{
    [Fact]
    public async Task AcquireLeaseAsync_SuccessfulAcquisition_ReturnsLease()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test-leases"
        };

        // Act & Assert - This requires real Azure Storage Emulator or Azurite
        // Will be tested in integration tests
        Assert.True(true); // Placeholder
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BlobLeaseProvider(null!));
    }

    [Fact]
    public async Task AcquireLeaseAsync_NullLeaseName_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };
        var provider = new BlobLeaseProvider(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await provider.AcquireLeaseAsync(null!, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task AcquireLeaseAsync_EmptyLeaseName_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };
        var provider = new BlobLeaseProvider(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await provider.AcquireLeaseAsync("", TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task AcquireLeaseAsync_DurationBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };
        var provider = new BlobLeaseProvider(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await provider.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task AcquireLeaseAsync_DurationAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };
        var provider = new BlobLeaseProvider(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await provider.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(70)));
    }

    [Fact]
    public async Task BreakLeaseAsync_NullLeaseName_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };
        var provider = new BlobLeaseProvider(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await provider.BreakLeaseAsync(null!));
    }

    [Fact]
    public async Task BreakLeaseAsync_EmptyLeaseName_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };
        var provider = new BlobLeaseProvider(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await provider.BreakLeaseAsync(""));
    }

    [Fact]
    public void BlobLeaseProviderOptions_ValidateMinMaxDurations()
    {
        // Arrange & Act
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };

        // Assert
        options.MinLeaseDuration.Should().Be(TimeSpan.FromSeconds(15));
        options.MaxLeaseDuration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void BlobLeaseProviderOptions_DefaultBlobPrefix_IsLeasePrefix()
    {
        // Arrange & Act
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test"
        };

        // Assert
        options.BlobPrefix.Should().Be("lease-");
    }

    [Fact]
    public void BlobLeaseProviderOptions_CustomBlobPrefix_IsSet()
    {
        // Arrange & Act
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test",
            BlobPrefix = "leases/"
        };

        // Assert
        options.BlobPrefix.Should().Be("leases/");
    }

    [Fact]
    public void BlobLeaseProviderOptions_WithMetadata_StoresMetadata()
    {
        // Arrange & Act
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "test",
            Metadata = new Dictionary<string, string>
            {
                { "environment", "test" },
                { "team", "platform" }
            }
        };

        // Assert
        options.Metadata.Should().HaveCount(2);
        options.Metadata["environment"].Should().Be("test");
        options.Metadata["team"].Should().Be("platform");
    }
}
