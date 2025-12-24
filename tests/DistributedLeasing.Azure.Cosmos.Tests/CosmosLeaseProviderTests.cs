using DistributedLeasing.Abstractions.Exceptions;
using DistributedLeasing.Azure.Cosmos;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Xunit;

namespace DistributedLeasing.Azure.Cosmos.Tests;

public class CosmosLeaseProviderTests
{
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CosmosLeaseProvider(null!));
    }

    [Fact]
    public void CosmosLeaseProviderOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "leases",
            ContainerName = "leases"
        };

        // Assert
        options.DefaultTimeToLive.Should().Be(300); // 5 minutes, not 3600
        options.CreateDatabaseIfNotExists.Should().BeTrue();
        options.CreateContainerIfNotExists.Should().BeTrue();
    }

    [Fact]
    public void CosmosLeaseProviderOptions_WithMetadata_StoresMetadata()
    {
        // Arrange & Act
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "leases",
            ContainerName = "leases",
            Metadata = new Dictionary<string, string>
            {
                { "region", "us-west" },
                { "env", "production" }
            }
        };

        // Assert
        options.Metadata.Should().HaveCount(2);
        options.Metadata["region"].Should().Be("us-west");
        options.Metadata["env"].Should().Be("production");
    }

    [Fact]
    public void CosmosLeaseProviderOptions_CustomTTL_IsSet()
    {
        // Arrange & Act
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "leases",
            ContainerName = "leases",
            DefaultTimeToLive = 7200
        };

        // Assert
        options.DefaultTimeToLive.Should().Be(7200);
    }

    [Fact]
    public void CosmosLeaseProviderOptions_Validate_WithoutConnectionStringOrEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new CosmosLeaseProviderOptions
        {
            DatabaseName = "leases",
            ContainerName = "leases"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CosmosLeaseProviderOptions_Validate_WithoutDatabaseName_ThrowsArgumentException()
    {
        // Arrange
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "", // Empty database name
            ContainerName = "leases"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void CosmosLeaseProviderOptions_Validate_WithoutContainerName_ThrowsArgumentException()
    {
        // Arrange
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "leases",
            ContainerName = "" // Empty container name
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void CosmosLeaseProviderOptions_Validate_WithValidConfiguration_Succeeds()
    {
        // Arrange
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "leases",
            ContainerName = "leases"
        };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Fact]
    public void CosmosLeaseProviderOptions_Validate_WithEndpointInsteadOfConnectionString_Succeeds()
    {
        // Arrange
        var options = new CosmosLeaseProviderOptions
        {
            Endpoint = new Uri("https://test.documents.azure.com:443/"),
            DatabaseName = "leases",
            ContainerName = "leases"
        };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Fact]
    public void CosmosLeaseProviderOptions_DefaultDatabaseName_IsSet()
    {
        // Arrange & Act
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;"
        };

        // Assert
        options.DatabaseName.Should().Be("DistributedLeasing");
        options.ContainerName.Should().Be("Leases");
    }

    [Fact]
    public void CosmosLeaseProviderOptions_CustomPartitionKeyPath_IsSet()
    {
        // Arrange & Act
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;",
            DatabaseName = "leases",
            ContainerName = "leases",
            PartitionKeyPath = "/customKey"
        };

        // Assert
        options.PartitionKeyPath.Should().Be("/customKey");
    }

    [Fact]
    public void CosmosLeaseProviderOptions_DefaultPartitionKeyPath_IsId()
    {
        // Arrange & Act
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;"
        };

        // Assert
        options.PartitionKeyPath.Should().Be("/id");
    }

    [Fact]
    public void CosmosLeaseProviderOptions_ProvisionedThroughput_DefaultIs400()
    {
        // Arrange & Act
        var options = new CosmosLeaseProviderOptions
        {
            ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test;"
        };

        // Assert
        options.ProvisionedThroughput.Should().Be(400);
    }
}
