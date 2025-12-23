using DistributedLeasing.Authentication;
using FluentAssertions;

namespace DistributedLeasing.Authentication.Tests;

/// <summary>
/// Unit tests for <see cref="AuthenticationOptions"/>.
/// </summary>
public class AuthenticationOptionsTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var options = new AuthenticationOptions();

        // Assert
        options.Mode.Should().BeNull();
        options.ManagedIdentity.Should().BeNull();
        options.WorkloadIdentity.Should().BeNull();
        options.ServicePrincipal.Should().BeNull();
        options.FederatedCredential.Should().BeNull();
        options.EnableDevelopmentCredentials.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullMode_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new AuthenticationOptions();

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication.Mode is required*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyMode_ThrowsInvalidOperationException(string mode)
    {
        // Arrange
        var options = new AuthenticationOptions { Mode = mode };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication.Mode is required*");
    }

    [Fact]
    public void Validate_WithAutoMode_DoesNotThrow()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Auto
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithManagedIdentityMode_DoesNotThrow()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ManagedIdentity
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithWorkloadIdentityMode_DoesNotThrow()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.WorkloadIdentity
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithDevelopmentMode_DoesNotThrow()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Development
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithServicePrincipalMode_RequiresTenantId()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                ClientId = "client-id",
                ClientSecret = "secret"
                // TenantId missing
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TenantId is required*");
    }

    [Fact]
    public void Validate_WithServicePrincipalMode_RequiresClientId()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                TenantId = "tenant-id",
                ClientSecret = "secret"
                // ClientId missing
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ClientId is required*");
    }

    [Fact]
    public void Validate_WithServicePrincipalMode_RequiresCertificateOrSecret()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id"
                // Both CertificatePath and ClientSecret missing
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CertificatePath or ClientSecret*");
    }

    [Fact]
    public void Validate_WithServicePrincipalModeAndCertificate_DoesNotThrow()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                CertificatePath = "/path/to/cert.pfx"
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithServicePrincipalModeAndSecret_DoesNotThrow()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "secret"
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithFederatedCredentialMode_RequiresTenantId()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.FederatedCredential,
            FederatedCredential = new FederatedCredentialOptions
            {
                ClientId = "client-id",
                TokenFilePath = "/path/to/token"
                // TenantId missing
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TenantId is required*");
    }

    [Fact]
    public void Validate_WithFederatedCredentialMode_RequiresClientId()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.FederatedCredential,
            FederatedCredential = new FederatedCredentialOptions
            {
                TenantId = "tenant-id",
                TokenFilePath = "/path/to/token"
                // ClientId missing
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ClientId is required*");
    }

    [Fact]
    public void Validate_WithFederatedCredentialMode_RequiresTokenFilePath()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.FederatedCredential,
            FederatedCredential = new FederatedCredentialOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id"
                // TokenFilePath missing
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TokenFilePath is required*");
    }

    [Fact]
    public void Validate_WithFederatedCredentialModeAndAllRequired_DoesNotThrow()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.FederatedCredential,
            FederatedCredential = new FederatedCredentialOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                TokenFilePath = "/path/to/token"
            }
        };

        // Act
        Action act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ManagedIdentityOptions_ClientId_CanBeNull()
    {
        // Arrange & Act
        var options = new ManagedIdentityOptions();

        // Assert
        options.ClientId.Should().BeNull();
    }

    [Fact]
    public void ManagedIdentityOptions_ClientId_CanBeSet()
    {
        // Arrange
        var clientId = "12345678-1234-1234-1234-123456789012";
        var options = new ManagedIdentityOptions { ClientId = clientId };

        // Act & Assert
        options.ClientId.Should().Be(clientId);
    }

    [Fact]
    public void WorkloadIdentityOptions_AllPropertiesOptional()
    {
        // Arrange & Act
        var options = new WorkloadIdentityOptions();

        // Assert
        options.TenantId.Should().BeNull();
        options.ClientId.Should().BeNull();
        options.TokenFilePath.Should().BeNull();
    }
}
