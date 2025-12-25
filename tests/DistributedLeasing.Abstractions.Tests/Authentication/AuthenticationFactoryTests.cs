using DistributedLeasing.Abstractions.Authentication;
using DistributedLeasing.Tests.Shared;
using DistributedLeasing.Tests.Shared.Fixtures;
using FluentAssertions;
using Xunit;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace DistributedLeasing.Abstractions.Tests.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthenticationFactory"/>.
/// </summary>
public class AuthenticationFactoryTests : IClassFixture<LoggingFixture>
{
    private readonly LoggingFixture _loggingFixture;

    public AuthenticationFactoryTests(LoggingFixture loggingFixture)
    {
        _loggingFixture = loggingFixture;
    }

    [Fact]
    public void CreateCredential_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new AuthenticationFactory();

        // Act
        Action act = () => factory.CreateCredential(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void CreateCredential_ManagedIdentity_SystemAssigned_ReturnsCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory();
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
    public void CreateCredential_ManagedIdentity_UserAssigned_ReturnsCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var clientId = Guid.NewGuid().ToString();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ManagedIdentity,
            ManagedIdentity = new ManagedIdentityOptions
            {
                ClientId = clientId
            }
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<ManagedIdentityCredential>();
    }

    [Fact]
    public void CreateCredential_WorkloadIdentity_ReturnsCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.WorkloadIdentity,
            WorkloadIdentity = new WorkloadIdentityOptions
            {
                TenantId = "test-tenant",
                ClientId = "test-client",
                TokenFilePath = "/tmp/test-token"
            }
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<WorkloadIdentityCredential>();
    }

    [Fact]
    public void CreateCredential_ServicePrincipal_WithSecret_ReturnsCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                TenantId = "test-tenant",
                ClientId = "test-client",
                ClientSecret = "test-secret"
            }
        };

        // Act
        var credential = factory.CreateCredential(options);

        // Assert
        credential.Should().NotBeNull();
        credential.Should().BeOfType<ClientSecretCredential>();
    }

    [Fact]
    public void CreateCredential_ServicePrincipal_WithoutSecretOrCert_ThrowsArgumentException()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ServicePrincipal,
            ServicePrincipal = new ServicePrincipalOptions
            {
                TenantId = "test-tenant",
                ClientId = "test-client"
            }
        };

        // Act
        Action act = () => factory.CreateCredential(options);

        // Assert - ArgumentException is thrown during validation
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateCredential_Development_InDevelopmentEnvironment_ReturnsChainedCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Development
        };

        // Save current environment variable
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            // Act
            var credential = factory.CreateCredential(options);

            // Assert
            credential.Should().NotBeNull();
            credential.Should().BeOfType<ChainedTokenCredential>();
        }
        finally
        {
            // Restore environment variable
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void CreateCredential_Development_InProductionEnvironment_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Development
        };

        // Save current environment variable
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            // Act
            Action act = () => factory.CreateCredential(options);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Production*");
        }
        finally
        {
            // Restore environment variable
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void CreateCredential_Auto_ReturnsChainedCredential()
    {
        // Arrange
        var factory = new AuthenticationFactory();
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
    public void CreateCredential_Auto_WithDevelopmentCredentials_IncludesDevelopmentChain()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.Auto,
            EnableDevelopmentCredentials = true
        };

        // Save current environment variable
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            // Act
            var credential = factory.CreateCredential(options);

            // Assert
            credential.Should().NotBeNull();
            credential.Should().BeOfType<ChainedTokenCredential>();
        }
        finally
        {
            // Restore environment variable
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void ValidateConfiguration_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new AuthenticationFactory();

        // Act
        Action act = () => factory.ValidateConfiguration(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateConfiguration_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var factory = new AuthenticationFactory();
        var options = new AuthenticationOptions
        {
            Mode = AuthenticationModes.ManagedIdentity
        };

        // Act
        Action act = () => factory.ValidateConfiguration(options);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateCredential_WithLogger_DoesNotThrow()
    {
        // Arrange
        var logger = _loggingFixture.CreateLogger<AuthenticationFactory>();
        var factory = new AuthenticationFactory(logger);
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
}
