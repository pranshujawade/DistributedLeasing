using DistributedLeasing.Authentication;
using FluentAssertions;

namespace DistributedLeasing.Authentication.Tests;

/// <summary>
/// Unit tests for <see cref="AuthenticationModes"/>.
/// </summary>
public class AuthenticationModesTests
{
    [Fact]
    public void Auto_HasExpectedValue()
    {
        // Assert
        AuthenticationModes.Auto.Should().Be("Auto");
    }

    [Fact]
    public void ManagedIdentity_HasExpectedValue()
    {
        // Assert
        AuthenticationModes.ManagedIdentity.Should().Be("ManagedIdentity");
    }

    [Fact]
    public void WorkloadIdentity_HasExpectedValue()
    {
        // Assert
        AuthenticationModes.WorkloadIdentity.Should().Be("WorkloadIdentity");
    }

    [Fact]
    public void ServicePrincipal_HasExpectedValue()
    {
        // Assert
        AuthenticationModes.ServicePrincipal.Should().Be("ServicePrincipal");
    }

    [Fact]
    public void FederatedCredential_HasExpectedValue()
    {
        // Assert
        AuthenticationModes.FederatedCredential.Should().Be("FederatedCredential");
    }

    [Fact]
    public void Development_HasExpectedValue()
    {
        // Assert
        AuthenticationModes.Development.Should().Be("Development");
    }

    [Fact]
    public void AllModes_AreUnique()
    {
        // Arrange
        var modes = new[]
        {
            AuthenticationModes.Auto,
            AuthenticationModes.ManagedIdentity,
            AuthenticationModes.WorkloadIdentity,
            AuthenticationModes.ServicePrincipal,
            AuthenticationModes.FederatedCredential,
            AuthenticationModes.Development
        };

        // Assert
        modes.Should().OnlyHaveUniqueItems();
    }
}
