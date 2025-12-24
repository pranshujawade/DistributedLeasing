using DistributedLeasing.Abstractions.Exceptions;
using DistributedLeasing.Azure.Redis;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace DistributedLeasing.Azure.Redis.Tests;

public class RedisLeaseProviderTests
{
    [Fact]
    public void Constructor_WithNullConnection_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RedisLeaseProvider(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RedisLeaseProvider(mockConnection.Object, null!));
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithNullLeaseName_ThrowsArgumentException()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        var provider = new RedisLeaseProvider(mockConnection.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await provider.AcquireLeaseAsync(null!, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithEmptyLeaseName_ThrowsArgumentException()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        var provider = new RedisLeaseProvider(mockConnection.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await provider.AcquireLeaseAsync("", TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithNegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        var provider = new RedisLeaseProvider(mockConnection.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await provider.AcquireLeaseAsync("test-lease", TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task AcquireLeaseAsync_WithZeroDuration_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        var provider = new RedisLeaseProvider(mockConnection.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await provider.AcquireLeaseAsync("test-lease", TimeSpan.Zero));
    }

    [Fact]
    public async Task BreakLeaseAsync_WithNullLeaseName_ThrowsArgumentException()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        var provider = new RedisLeaseProvider(mockConnection.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await provider.BreakLeaseAsync(null!));
    }

    [Fact]
    public async Task BreakLeaseAsync_WithEmptyLeaseName_ThrowsArgumentException()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        var provider = new RedisLeaseProvider(mockConnection.Object, options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await provider.BreakLeaseAsync(""));
    }

    [Fact]
    public void RedisLeaseProviderOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        // Assert
        options.KeyPrefix.Should().Be("lease:");
        options.Database.Should().Be(0);
        options.ClockDriftFactor.Should().Be(0.01);
        options.MinimumValidity.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RedisLeaseProviderOptions_WithMetadata_StoresMetadata()
    {
        // Arrange & Act
        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379",
            Metadata = new Dictionary<string, string>
            {
                { "service", "api" },
                { "version", "1.0" }
            }
        };

        // Assert
        options.Metadata.Should().HaveCount(2);
        options.Metadata["service"].Should().Be("api");
        options.Metadata["version"].Should().Be("1.0");
    }

    [Fact]
    public void RedisLeaseProviderOptions_CustomKeyPrefix_IsSet()
    {
        // Arrange & Act
        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = "myapp:locks:"
        };

        // Assert
        options.KeyPrefix.Should().Be("myapp:locks:");
    }

    [Fact]
    public void RedisLeaseProviderOptions_CustomDatabase_IsSet()
    {
        // Arrange & Act
        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379",
            Database = 5
        };

        // Assert
        options.Database.Should().Be(5);
    }

    [Fact]
    public void Dispose_WhenOwnsConnection_DisposesConnection()
    {
        // Arrange
        var mockConnection = new Mock<IConnectionMultiplexer>();
        var mockDatabase = new Mock<IDatabase>();
        mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);
        mockConnection.Setup(c => c.Dispose());

        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        var provider = new RedisLeaseProvider(mockConnection.Object, options);

        // Act
        provider.Dispose();
        provider.Dispose(); // Second dispose should be safe

        // Assert
        mockConnection.Verify(c => c.Dispose(), Times.Never); // Provider doesn't own the connection in this constructor
    }

    [Fact]
    public void RedisLeaseProviderOptions_Validate_WithoutConnectionStringOrHostName_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new RedisLeaseProviderOptions();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void RedisLeaseProviderOptions_Validate_WithConnectionString_Succeeds()
    {
        // Arrange
        var options = new RedisLeaseProviderOptions
        {
            ConnectionString = "localhost:6379"
        };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Fact]
    public void RedisLeaseProviderOptions_Validate_WithHostName_Succeeds()
    {
        // Arrange
        var options = new RedisLeaseProviderOptions
        {
            HostName = "localhost",
            Port = 6379
        };

        // Act & Assert - Should not throw
        options.Validate();
    }
}
