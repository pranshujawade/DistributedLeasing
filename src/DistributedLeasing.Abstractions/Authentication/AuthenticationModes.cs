namespace DistributedLeasing.Abstractions.Authentication
{
    /// <summary>
    /// Defines standard authentication mode values for configuration.
    /// </summary>
    /// <remarks>
    /// These constants are used in appsettings.json to specify which authentication method to use.
    /// The authentication library will automatically create the appropriate credential based on the selected mode.
    /// </remarks>
    public static class AuthenticationModes
    {
        /// <summary>
        /// Automatically detect and use the most appropriate authentication method based on the environment.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In production environments, attempts credentials in this order:
        /// <list type="number">
        /// <item>Workload Identity (if in Kubernetes or GitHub Actions)</item>
        /// <item>Managed Identity (if in Azure)</item>
        /// <item>Service Principal (if configured)</item>
        /// </list>
        /// </para>
        /// <para>
        /// In development environments, also includes:
        /// <list type="number">
        /// <item>Azure CLI</item>
        /// <item>Visual Studio</item>
        /// <item>Visual Studio Code</item>
        /// </list>
        /// </para>
        /// </remarks>
        public const string Auto = "Auto";

        /// <summary>
        /// Use Azure Managed Identity for authentication.
        /// </summary>
        /// <remarks>
        /// Supports both system-assigned and user-assigned managed identities.
        /// For user-assigned, specify the ClientId in ManagedIdentity configuration section.
        /// </remarks>
        public const string ManagedIdentity = "ManagedIdentity";

        /// <summary>
        /// Use Workload Identity for authentication (Kubernetes or GitHub Actions).
        /// </summary>
        /// <remarks>
        /// Requires environment variables: AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURE_FEDERATED_TOKEN_FILE.
        /// These are typically set automatically by AKS or GitHub Actions OIDC configuration.
        /// </remarks>
        public const string WorkloadIdentity = "WorkloadIdentity";

        /// <summary>
        /// Use Service Principal with certificate or client secret for authentication.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Certificate-based authentication (recommended):
        /// Provide TenantId, ClientId, and CertificatePath in ServicePrincipal configuration section.
        /// </para>
        /// <para>
        /// Client secret-based authentication (not recommended for production):
        /// Provide TenantId, ClientId, and ClientSecret in ServicePrincipal configuration section.
        /// The library will log a warning recommending certificate-based authentication instead.
        /// </para>
        /// </remarks>
        public const string ServicePrincipal = "ServicePrincipal";

        /// <summary>
        /// Use Federated Identity Credential for authentication.
        /// </summary>
        /// <remarks>
        /// Exchanges an external OIDC token for an Azure AD token.
        /// Requires TenantId, ClientId, and TokenFilePath in FederatedCredential configuration section.
        /// </remarks>
        public const string FederatedCredential = "FederatedCredential";

        /// <summary>
        /// Use development credentials only (Azure CLI, Visual Studio, VS Code).
        /// </summary>
        /// <remarks>
        /// Only works in development environments. Automatically blocked in production.
        /// Attempts credentials in order: Azure CLI → Visual Studio → Visual Studio Code.
        /// </remarks>
        public const string Development = "Development";
    }
}
