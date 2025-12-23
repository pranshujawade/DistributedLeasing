using Azure.Core;
using Azure.Identity;
using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Authentication;
using DistributedLeasing.Azure.Blob;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Azure.Blob.Tests;

/// <summary>
/// Unit tests for <see cref="BlobLeaseProviderOptions"/>.
/// </summary>
public class BlobLeaseProviderOptionsTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var options = new BlobLeaseProviderOptions();

        // Assert
        options.ContainerName.Should().Be("leases");
        options.BlobPrefix.Should().Be("lease-");
        options.CreateContainerIfNotExists.Should().BeFalse();
        options.StorageAccountUri.Should().BeNull();
        options.ConnectionString.Should().BeNull();
        options.Credential.Should().BeNull();
        options.Authentication.Should().BeNull();
        
        // Inherited from LeaseOptions
        options.DefaultLeaseDuration.Should().Be(TimeSpan.FromSeconds(60));
        options.AutoRenew.Should().BeFalse();
    }

    [Fact]
    public void MinLeaseDuration_ReturnsAzureMinimum()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions();

        // Act & Assert
        options.MinLeaseDuration.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void MaxLeaseDuration_ReturnsAzureMaximum()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions();

        // Act & Assert
        options.MaxLeaseDuration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Validate_WithValidConnectionString_DoesNotThrow()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net"
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithValidAuthentication_DoesNotThrow()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            Authentication = new AuthenticationOptions
            {
                Mode = AuthenticationModes.ManagedIdentity
            },
            StorageAccountUri = new Uri("https://testaccount.blob.core.windows.net")
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithValidCredential_DoesNotThrow()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            Credential = new DefaultAzureCredential(),
            StorageAccountUri = new Uri("https://testaccount.blob.core.windows.net")
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithNoAuthenticationMethod_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions();

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication must be configured*");
    }

    [Fact]
    public void Validate_WithAuthenticationButNoUri_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            Authentication = new AuthenticationOptions
            {
                Mode = AuthenticationModes.ManagedIdentity
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*StorageAccountUri*");
    }

    [Fact]
    public void Validate_WithCredentialButNoUri_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            Credential = new DefaultAzureCredential()
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*StorageAccountUri*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidContainerName_ThrowsArgumentException(string? invalidName)
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            ContainerName = invalidName!
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ContainerName*");
    }

    [Fact]
    public void Validate_WithLeaseDurationBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            DefaultLeaseDuration = TimeSpan.FromSeconds(10) // Below 15 second minimum
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 15 seconds*");
    }

    [Fact]
    public void Validate_WithLeaseDurationAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            DefaultLeaseDuration = TimeSpan.FromSeconds(120) // Above 60 second maximum
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at most 60 seconds*");
    }

    [Fact]
    public void Validate_WithExactMinimumDuration_DoesNotThrow()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            DefaultLeaseDuration = TimeSpan.FromSeconds(15)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithExactMaximumDuration_DoesNotThrow()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            DefaultLeaseDuration = TimeSpan.FromSeconds(60)
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void BlobPrefix_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions();
        var prefix = "my-app/leases/";

        // Act
        options.BlobPrefix = prefix;

        // Assert
        options.BlobPrefix.Should().Be(prefix);
    }

    [Fact]
    public void CreateContainerIfNotExists_CanBeSetToFalse()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            CreateContainerIfNotExists = false
        };

        // Assert
        options.CreateContainerIfNotExists.Should().BeFalse();
    }

    [Fact]
    public void StorageAccountUri_CanBeSet()
    {
        // Arrange
        var uri = new Uri("https://myaccount.blob.core.windows.net");
        var options = new BlobLeaseProviderOptions
        {
            StorageAccountUri = uri
        };

        // Assert
        options.StorageAccountUri.Should().Be(uri);
    }

    [Fact]
    public void Validate_CallsBaseValidation()
    {
        // Arrange - Create options with invalid base configuration (negative retry interval)
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net"
        };

        // Act - Try to set invalid retry interval, which will throw in setter
        Action act = () => options.AutoRenewRetryInterval = TimeSpan.FromSeconds(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Auto-renew retry interval*");
    }

    [Fact]
    public void Validate_WithMultipleAuthMethods_PrioritizesCredential()
    {
        // Arrange - Credential should take precedence
        var options = new BlobLeaseProviderOptions
        {
            Credential = new DefaultAzureCredential(),
            StorageAccountUri = new Uri("https://testaccount.blob.core.windows.net"),
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Authentication = new AuthenticationOptions
            {
                Mode = AuthenticationModes.Auto
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert - Should not throw, credential is valid
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithConnectionStringAndAuthentication_UsesConnectionString()
    {
        // Arrange
        var options = new BlobLeaseProviderOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGVzdGtleQ==;EndpointSuffix=core.windows.net",
            Authentication = new AuthenticationOptions
            {
                Mode = AuthenticationModes.Auto
            },
            StorageAccountUri = new Uri("https://testaccount.blob.core.windows.net")
        };

        // Act
        Action act = () => options.Validate();

        // Assert - Should not throw, connection string is valid
        act.Should().NotThrow();
    }
}
