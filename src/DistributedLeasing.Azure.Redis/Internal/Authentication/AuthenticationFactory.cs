using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistributedLeasing.Azure.Redis.Internal.Authentication
{
    /// <summary>
    /// Factory for creating Azure credentials automatically from configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used internally by the authentication library and providers
    /// to create credentials based on the configured authentication mode.
    /// All token acquisition and refresh is handled automatically.
    /// </para>
    /// <para>
    /// Generally, you should not need to use this class directly. Use the
    /// <see cref="AuthenticationServiceExtensions.AddAzureAuthentication"/> extension method instead.
    /// </para>
    /// </remarks>
    internal class AuthenticationFactory : IAuthenticationFactory
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationFactory"/> class.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics. If null, uses NullLogger.</param>
        public AuthenticationFactory(ILogger? logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <inheritdoc/>
        public TokenCredential CreateCredential(AuthenticationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // Validate configuration first
            ValidateConfiguration(options);

            _logger.LogDebug("Creating credential for Mode: {Mode}", options.Mode);

            // Create credential based on mode
            TokenCredential credential = options.Mode switch
            {
                AuthenticationModes.ManagedIdentity => CreateManagedIdentityCredential(options),
                AuthenticationModes.WorkloadIdentity => CreateWorkloadIdentityCredential(options),
                AuthenticationModes.ServicePrincipal => CreateServicePrincipalCredential(options),
                AuthenticationModes.FederatedCredential => CreateFederatedCredential(options),
                AuthenticationModes.Development => CreateDevelopmentCredential(options),
                AuthenticationModes.Auto => CreateAutoCredential(options),
                _ => throw new InvalidOperationException($"Unknown authentication mode: {options.Mode}")
            };

            _logger.LogInformation("Successfully created {CredentialType} for Mode: {Mode}", 
                credential.GetType().Name, options.Mode);

            return credential;
        }

        /// <inheritdoc/>
        public void ValidateConfiguration(AuthenticationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();
        }

        private TokenCredential CreateManagedIdentityCredential(AuthenticationOptions options)
        {
            var clientId = options.ManagedIdentity?.ClientId;

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogDebug("Creating system-assigned ManagedIdentityCredential");
                return new ManagedIdentityCredential();
            }
            else
            {
                _logger.LogDebug("Creating user-assigned ManagedIdentityCredential with ClientId: {ClientId}", clientId);
                return new ManagedIdentityCredential(clientId);
            }
        }

        private TokenCredential CreateWorkloadIdentityCredential(AuthenticationOptions options)
        {
            var workloadOptions = new global::Azure.Identity.WorkloadIdentityCredentialOptions();

            // Use configuration values if provided, otherwise environment variables are used automatically
            if (options.WorkloadIdentity != null)
            {
                if (!string.IsNullOrWhiteSpace(options.WorkloadIdentity.TenantId))
                {
                    workloadOptions.TenantId = options.WorkloadIdentity.TenantId;
                }
                if (!string.IsNullOrWhiteSpace(options.WorkloadIdentity.ClientId))
                {
                    workloadOptions.ClientId = options.WorkloadIdentity.ClientId;
                }
                if (!string.IsNullOrWhiteSpace(options.WorkloadIdentity.TokenFilePath))
                {
                    workloadOptions.TokenFilePath = options.WorkloadIdentity.TokenFilePath;
                }
            }

            _logger.LogDebug("Creating WorkloadIdentityCredential");
            return new WorkloadIdentityCredential(workloadOptions);
        }

        private TokenCredential CreateServicePrincipalCredential(AuthenticationOptions options)
        {
            var sp = options.ServicePrincipal!; // Already validated

            // Prioritize certificate over client secret
            if (!string.IsNullOrWhiteSpace(sp.CertificatePath))
            {
                _logger.LogDebug("Creating ClientCertificateCredential for TenantId: {TenantId}, ClientId: {ClientId}", 
                    sp.TenantId, sp.ClientId);
                
                // Check if certificate file exists
                if (!System.IO.File.Exists(sp.CertificatePath))
                {
                    throw new System.IO.FileNotFoundException(
                        $"Certificate file '{sp.CertificatePath}' not found. Verify CertificatePath in appsettings.json.",
                        sp.CertificatePath);
                }

                if (!string.IsNullOrWhiteSpace(sp.CertificatePassword))
                {
                    return new ClientCertificateCredential(sp.TenantId, sp.ClientId, sp.CertificatePath, 
                        new ClientCertificateCredentialOptions { });
                }
                else
                {
                    return new ClientCertificateCredential(sp.TenantId, sp.ClientId, sp.CertificatePath);
                }
            }
            else if (!string.IsNullOrWhiteSpace(sp.ClientSecret))
            {
                _logger.LogWarning("Using ClientSecretCredential. Certificate-based authentication is recommended for production. " +
                    "TenantId: {TenantId}, ClientId: {ClientId}", sp.TenantId, sp.ClientId);
                
                return new ClientSecretCredential(sp.TenantId, sp.ClientId, sp.ClientSecret);
            }
            else
            {
                throw new InvalidOperationException("Either CertificatePath or ClientSecret must be provided for Service Principal authentication.");
            }
        }

        private TokenCredential CreateFederatedCredential(AuthenticationOptions options)
        {
            var fed = options.FederatedCredential!; // Already validated

            _logger.LogDebug("Creating WorkloadIdentityCredential for federated credential. TenantId: {TenantId}, ClientId: {ClientId}", 
                fed.TenantId, fed.ClientId);

            // Check if token file exists
            if (!System.IO.File.Exists(fed.TokenFilePath))
            {
                throw new System.IO.FileNotFoundException(
                    $"Token file '{fed.TokenFilePath}' not found. Verify TokenFilePath in appsettings.json.",
                    fed.TokenFilePath);
            }

            var workloadOptions = new global::Azure.Identity.WorkloadIdentityCredentialOptions
            {
                TenantId = fed.TenantId,
                ClientId = fed.ClientId,
                TokenFilePath = fed.TokenFilePath
            };

            if (!string.IsNullOrWhiteSpace(fed.Authority))
            {
                workloadOptions.AuthorityHost = new Uri(fed.Authority);
            }

            return new WorkloadIdentityCredential(workloadOptions);
        }

        private TokenCredential CreateDevelopmentCredential(AuthenticationOptions options)
        {
            _logger.LogDebug("Creating development credential chain (AzureCli → VisualStudio → VisualStudioCode)");

            // Check if we're in a production environment
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            
            if (!string.IsNullOrEmpty(environment) && 
                (environment.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
                 environment.Equals("Staging", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Development credentials cannot be used in {environment} environment. Use ManagedIdentity, WorkloadIdentity, or ServicePrincipal instead.");
            }

            return new ChainedTokenCredential(
                new AzureCliCredential(),
                new VisualStudioCredential(),
                new VisualStudioCodeCredential()
            );
        }

        private TokenCredential CreateAutoCredential(AuthenticationOptions options)
        {
            _logger.LogDebug("Creating automatic credential chain based on environment");

            // Detect environment and create appropriate credential chain
            var credentials = new System.Collections.Generic.List<TokenCredential>();

            // Add environment credential (checks for environment variables)
            credentials.Add(new EnvironmentCredential());

            // Add workload identity if environment variables are present
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var tokenFile = Environment.GetEnvironmentVariable("AZURE_FEDERATED_TOKEN_FILE");
            
            if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(tokenFile))
            {
                _logger.LogDebug("Detected workload identity environment variables, adding WorkloadIdentityCredential to chain");
                credentials.Add(new WorkloadIdentityCredential());
            }

            // Add managed identity
            if (!string.IsNullOrWhiteSpace(options.ManagedIdentity?.ClientId))
            {
                _logger.LogDebug("Adding user-assigned ManagedIdentityCredential to chain");
                credentials.Add(new ManagedIdentityCredential(options.ManagedIdentity!.ClientId));
            }
            else
            {
                _logger.LogDebug("Adding system-assigned ManagedIdentityCredential to chain");
                credentials.Add(new ManagedIdentityCredential());
            }

            // Add development credentials if enabled and not in production
            if (options.EnableDevelopmentCredentials)
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
                
                if (string.IsNullOrEmpty(environment) || 
                    (!environment.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
                     !environment.Equals("Staging", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("Adding development credentials to chain (Azure CLI, Visual Studio, VS Code)");
                    credentials.Add(new AzureCliCredential());
                    credentials.Add(new VisualStudioCredential());
                    credentials.Add(new VisualStudioCodeCredential());
                }
            }

            return new ChainedTokenCredential(credentials.ToArray());
        }
    }
}
