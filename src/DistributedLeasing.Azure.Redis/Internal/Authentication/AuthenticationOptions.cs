using System;

namespace DistributedLeasing.Azure.Redis.Internal.Authentication
{
    /// <summary>
    /// Configuration options for automatic authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides configuration-driven authentication for Azure resources.
    /// All authentication is handled automatically based on the Mode setting in appsettings.json.
    /// </para>
    /// <para>
    /// <strong>Example - Managed Identity (System-Assigned):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "ManagedIdentity"
    /// }
    /// </code>
    /// <para>
    /// <strong>Example - Workload Identity (AKS):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "WorkloadIdentity"
    /// }
    /// </code>
    /// <para>
    /// <strong>Example - Service Principal (Certificate):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "ServicePrincipal",
    ///   "ServicePrincipal": {
    ///     "TenantId": "...",
    ///     "ClientId": "...",
    ///     "CertificatePath": "/path/to/cert.pem"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class AuthenticationOptions
    {
        /// <summary>
        /// Gets or sets the authentication mode.
        /// </summary>
        /// <value>
        /// The authentication mode. Must be one of: "Auto", "ManagedIdentity", "WorkloadIdentity", 
        /// "ServicePrincipal", "FederatedCredential", or "Development".
        /// </value>
        /// <remarks>
        /// <para>
        /// This is the primary setting that determines which authentication method to use.
        /// See <see cref="AuthenticationModes"/> for available values and their descriptions.
        /// </para>
        /// <para>
        /// The library will automatically create the appropriate credential based on this mode
        /// and handle all token acquisition and refresh automatically.
        /// </para>
        /// </remarks>
        public string? Mode { get; set; }

        /// <summary>
        /// Gets or sets the Managed Identity configuration.
        /// </summary>
        /// <value>
        /// Configuration for managed identity, or <c>null</c> if not using managed identity.
        /// </value>
        /// <remarks>
        /// Only used when Mode is "ManagedIdentity".
        /// Leave null for system-assigned identity or configure ClientId for user-assigned identity.
        /// </remarks>
        public ManagedIdentityOptions? ManagedIdentity { get; set; }

        /// <summary>
        /// Gets or sets the Workload Identity configuration.
        /// </summary>
        /// <value>
        /// Configuration for workload identity, or <c>null</c> if not using workload identity.
        /// </value>
        /// <remarks>
        /// Only used when Mode is "WorkloadIdentity".
        /// Typically not needed as environment variables are set automatically by AKS or GitHub Actions.
        /// </remarks>
        public WorkloadIdentityOptions? WorkloadIdentity { get; set; }

        /// <summary>
        /// Gets or sets the Service Principal configuration.
        /// </summary>
        /// <value>
        /// Configuration for service principal, or <c>null</c> if not using service principal.
        /// </value>
        /// <remarks>
        /// Only used when Mode is "ServicePrincipal".
        /// Requires TenantId, ClientId, and either CertificatePath or ClientSecret.
        /// </remarks>
        public ServicePrincipalOptions? ServicePrincipal { get; set; }

        /// <summary>
        /// Gets or sets the Federated Credential configuration.
        /// </summary>
        /// <value>
        /// Configuration for federated credentials, or <c>null</c> if not using federated credentials.
        /// </value>
        /// <remarks>
        /// Only used when Mode is "FederatedCredential".
        /// Requires TenantId, ClientId, and TokenFilePath.
        /// </remarks>
        public FederatedCredentialOptions? FederatedCredential { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable development credentials.
        /// </summary>
        /// <value>
        /// <c>true</c> to include development credentials (Azure CLI, Visual Studio, VS Code) in Auto mode;
        /// otherwise, <c>false</c>. Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When Mode is "Auto", this setting determines whether to include development credentials
        /// in the credential chain. Development credentials are automatically excluded in production environments.
        /// </para>
        /// <para>
        /// When Mode is "Development", development credentials are always used regardless of this setting.
        /// </para>
        /// </remarks>
        public bool EnableDevelopmentCredentials { get; set; } = true;

        /// <summary>
        /// Validates the authentication configuration.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when Mode is not specified or is invalid.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when required properties for the selected Mode are missing.
        /// </exception>
        public void Validate()
        {
            // Validate Mode is specified
            if (string.IsNullOrWhiteSpace(Mode))
            {
                throw new InvalidOperationException(
                    "Authentication.Mode is required. Specify: Auto, ManagedIdentity, WorkloadIdentity, ServicePrincipal, FederatedCredential, or Development.");
            }

            // Validate Mode is a known value
            if (Mode != AuthenticationModes.Auto &&
                Mode != AuthenticationModes.ManagedIdentity &&
                Mode != AuthenticationModes.WorkloadIdentity &&
                Mode != AuthenticationModes.ServicePrincipal &&
                Mode != AuthenticationModes.FederatedCredential &&
                Mode != AuthenticationModes.Development)
            {
                throw new ArgumentException(
                    $"Authentication.Mode '{Mode}' is invalid. Valid values: Auto, ManagedIdentity, WorkloadIdentity, ServicePrincipal, FederatedCredential, Development.",
                    nameof(Mode));
            }

            // Mode-specific validation
            switch (Mode)
            {
                case AuthenticationModes.ManagedIdentity:
                    // No required properties for managed identity - ClientId is optional
                    break;

                case AuthenticationModes.WorkloadIdentity:
                    // No required properties - environment variables are used if not configured
                    break;

                case AuthenticationModes.ServicePrincipal:
                    ValidateServicePrincipalOptions();
                    break;

                case AuthenticationModes.FederatedCredential:
                    ValidateFederatedCredentialOptions();
                    break;

                case AuthenticationModes.Auto:
                case AuthenticationModes.Development:
                    // No required properties for auto or development modes
                    break;
            }
        }

        private void ValidateServicePrincipalOptions()
        {
            if (ServicePrincipal == null)
            {
                throw new ArgumentException(
                    "ServicePrincipal configuration is required when Authentication.Mode is 'ServicePrincipal'. Check appsettings.json.",
                    nameof(ServicePrincipal));
            }

            if (string.IsNullOrWhiteSpace(ServicePrincipal.TenantId))
            {
                throw new ArgumentException(
                    "ServicePrincipal.TenantId is required when Authentication.Mode is 'ServicePrincipal'. Check appsettings.json.",
                    nameof(ServicePrincipal));
            }

            if (string.IsNullOrWhiteSpace(ServicePrincipal.ClientId))
            {
                throw new ArgumentException(
                    "ServicePrincipal.ClientId is required when Authentication.Mode is 'ServicePrincipal'. Check appsettings.json.",
                    nameof(ServicePrincipal));
            }

            if (string.IsNullOrWhiteSpace(ServicePrincipal.CertificatePath) && 
                string.IsNullOrWhiteSpace(ServicePrincipal.ClientSecret))
            {
                throw new ArgumentException(
                    "Either ServicePrincipal.CertificatePath or ServicePrincipal.ClientSecret is required when Authentication.Mode is 'ServicePrincipal'. Certificate is recommended. Check appsettings.json.",
                    nameof(ServicePrincipal));
            }
        }

        private void ValidateFederatedCredentialOptions()
        {
            if (FederatedCredential == null)
            {
                throw new ArgumentException(
                    "FederatedCredential configuration is required when Authentication.Mode is 'FederatedCredential'. Check appsettings.json.",
                    nameof(FederatedCredential));
            }

            if (string.IsNullOrWhiteSpace(FederatedCredential.TenantId))
            {
                throw new ArgumentException(
                    "FederatedCredential.TenantId is required when Authentication.Mode is 'FederatedCredential'. Check appsettings.json.",
                    nameof(FederatedCredential));
            }

            if (string.IsNullOrWhiteSpace(FederatedCredential.ClientId))
            {
                throw new ArgumentException(
                    "FederatedCredential.ClientId is required when Authentication.Mode is 'FederatedCredential'. Check appsettings.json.",
                    nameof(FederatedCredential));
            }

            if (string.IsNullOrWhiteSpace(FederatedCredential.TokenFilePath))
            {
                throw new ArgumentException(
                    "FederatedCredential.TokenFilePath is required when Authentication.Mode is 'FederatedCredential'. Check appsettings.json.",
                    nameof(FederatedCredential));
            }
        }
    }
}
