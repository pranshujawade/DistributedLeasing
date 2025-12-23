using Azure.Core;
using Azure.Identity;
using DistributedLeasing.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistributedLeasing.Authentication.Tests;

/// <summary>
/// Unit tests for <see cref="AuthenticationFactory"/>.
/// </summary>
public class AuthenticationFactoryTests
{
    [Fact]
    public void CreateCredential_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);

        // Act
        Action act = () => factory.CreateCredential(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void CreateCredential_WithInvalidMode_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions { Mode = "InvalidMode" };

        // Act
        Action act = () => factory.CreateCredential(options);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown authentication mode*");
    }

    [Fact]
    public void CreateCredential_WithAutoMode_ReturnsChainedTokenCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Auto
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<ChainedTokenCredential>();
    }

    [Fact]
    public void CreateCredential_WithManagedIdentityMode_SystemAssigned_ReturnsManagedIdentityCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ManagedIdentity
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<ManagedIdentityCredential>();
    }

    [Fact]
    public void CreateCredential_WithManagedIdentityMode_UserAssigned_ReturnsManagedIdentityCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ManagedIdentity,
            ManagedIdentity = new ManagedIdentityOptions
            {
                ClientId = "12345678-1234-1234-1234-123456789012"
            }
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<ManagedIdentityCredential>();
    }

    [Fact]
    public void CreateCredential_WithWorkloadIdentityMode_ReturnsWorkloadIdentityCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.WorkloadIdentity
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<WorkloadIdentityCredential>();
    }

    [Fact]
    public void CreateCredential_WithServicePrincipalMode_Certificate_ReturnsClientCertificateCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var tempCertPath = Path.GetTempFileName();
        
        try
        {
            // Create a dummy file to simulate certificate
            File.WriteAllText(tempCertPath, "dummy cert");
            
            var options = new AuthenticationOptions
            {
                Mode = AuthenticationModes.ServicePrincipal,
                ServicePrincipal = new ServicePrincipalOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "client-id",
                    CertificatePath = tempCertPath
                }
            };

            // Act
            var credential = factory.CreateCredential(options);

            // Assert
            credential.Should().NotBeNull();
            credential.Should().BeOfType<ClientCertificateCredential>();
        }
        finally
        {
            if (File.Exists(tempCertPath))
            {
                File.Delete(tempCertPath);
            }
        }
    }

    [Fact]
    public void CreateCredential_WithServicePrincipalMode_CertificateNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                CertificatePath = "/nonexistent/path/cert.pfx"
            }
        };

        // Act
        Action act = () => factory.CreateCredential(options);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*cert.pfx*not found*");
    }

    [Fact]
    public void CreateCredential_WithServicePrincipalMode_ClientSecret_ReturnsClientSecretCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
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
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<ClientSecretCredential>();
    }

    [Fact]
    public void CreateCredential_WithFederatedCredentialMode_ReturnsWorkloadIdentityCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var tempTokenPath = Path.GetTempFileName();
        
        try
        {
            // Create a dummy token file
            File.WriteAllText(tempTokenPath, "dummy token");
            
            var options = new AuthenticationOptions
            {
                Mode = AuthenticationModes.FederatedCredential,
                FederatedCredential = new FederatedCredentialOptions
                {
                    TenantId = "tenant-id",
                    ClientId = "client-id",
                    TokenFilePath = tempTokenPath
                }
            };

            // Act
            var credential = factory.CreateCredential(options);

            // Assert
            credential.Should().NotBeNull();
            credential.Should().BeOfType<WorkloadIdentityCredential>();
        }
        finally
        {
            if (File.Exists(tempTokenPath))
            {
                File.Delete(tempTokenPath);
            }
        }
    }

    [Fact]
    public void CreateCredential_WithFederatedCredentialMode_TokenFileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.FederatedCredential,
            FederatedCredential = new FederatedCredentialOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                TokenFilePath = "/nonexistent/path/token"
            }
        };

        // Act
        Action act = () => factory.CreateCredential(options);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*token*not found*");
    }

    [Fact]
    public void CreateCredential_WithDevelopmentMode_ReturnsChainedTokenCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Development
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<ChainedTokenCredential>();
    }

    [Fact]
    public void CreateCredential_WithDevelopmentMode_InProductionEnvironment_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Development
        };

        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            // Act
            Action act = () => factory.CreateCredential(options);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Development credentials cannot be used in Production environment*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void CreateCredential_WithDevelopmentMode_InStagingEnvironment_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Development
        };

        var originalEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Staging");

            // Act
            Action act = () => factory.CreateCredential(options);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Development credentials cannot be used in Staging environment*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void ValidateConfiguration_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);

        // Act
        Action act = () => factory.ValidateConfiguration(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateConfiguration_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var factory = new AuthenticationFactory(NullLogger.Instance);
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Auto
        };

        // Act
        Action act = () => factory.ValidateConfiguration(options);

        // Assert
        act.Should().NotThrow();
    }
}
