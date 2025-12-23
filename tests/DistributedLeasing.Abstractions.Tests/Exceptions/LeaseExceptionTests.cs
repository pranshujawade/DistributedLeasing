using DistributedLeasing.Abstractions.Exceptions;
using FluentAssertions;
using Xunit;

namespace DistributedLeasing.Abstractions.Tests.Exceptions;

/// <summary>
/// Unit tests for the lease exception hierarchy.
/// </summary>
public class LeaseExceptionTests
{
    [Fact]
    public void LeaseException_CanBeCreatedWithMessage()
    {
        // Arrange
        var message = "Test exception message";

        // Act
        var exception = new LeaseException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void LeaseException_CanBeCreatedWithMessageAndInnerException()
    {
        // Arrange
        var message = "Test exception message";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new LeaseException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void LeaseException_CanSetLeaseName()
    {
        // Arrange
        var exception = new LeaseException("Test");
        var leaseName = "my-lease";

        // Act
        exception.LeaseName = leaseName;

        // Assert
        exception.LeaseName.Should().Be(leaseName);
    }

    [Fact]
    public void LeaseException_CanSetLeaseId()
    {
        // Arrange
        var exception = new LeaseException("Test");
        var leaseId = Guid.NewGuid().ToString();

        // Act
        exception.LeaseId = leaseId;

        // Assert
        exception.LeaseId.Should().Be(leaseId);
    }

    [Fact]
    public void LeaseAcquisitionException_InheritsFromLeaseException()
    {
        // Arrange & Act
        var exception = new LeaseAcquisitionException("Test");

        // Assert
        exception.Should().BeAssignableTo<LeaseException>();
    }

    [Fact]
    public void LeaseRenewalException_InheritsFromLeaseException()
    {
        // Arrange & Act
        var exception = new LeaseRenewalException("Test");

        // Assert
        exception.Should().BeAssignableTo<LeaseException>();
    }

    [Fact]
    public void LeaseConflictException_InheritsFromLeaseException()
    {
        // Arrange & Act
        var exception = new LeaseConflictException("Test");

        // Assert
        exception.Should().BeAssignableTo<LeaseException>();
    }

    [Fact]
    public void LeaseLostException_InheritsFromLeaseException()
    {
        // Arrange & Act
        var exception = new LeaseLostException("Test");

        // Assert
        exception.Should().BeAssignableTo<LeaseException>();
    }

    [Fact]
    public void ProviderUnavailableException_InheritsFromLeaseException()
    {
        // Arrange & Act
        var exception = new ProviderUnavailableException("Test");

        // Assert
        exception.Should().BeAssignableTo<LeaseException>();
    }

    [Fact]
    public void ProviderUnavailableException_CanSetProviderName()
    {
        // Arrange
        var exception = new ProviderUnavailableException("Test");
        var providerName = "BlobLeaseProvider";

        // Act
        exception.ProviderName = providerName;

        // Assert
        exception.ProviderName.Should().Be(providerName);
    }

    [Fact]
    public void LeaseAcquisitionException_PreservesContextInformation()
    {
        // Arrange
        var leaseName = "test-lease";
        var leaseId = Guid.NewGuid().ToString();
        var innerException = new TimeoutException("Timed out");

        // Act
        var exception = new LeaseAcquisitionException("Failed to acquire", innerException)
        {
            LeaseName = leaseName,
            LeaseId = leaseId
        };

        // Assert
        exception.LeaseName.Should().Be(leaseName);
        exception.LeaseId.Should().Be(leaseId);
        exception.InnerException.Should().Be(innerException);
    }
}
