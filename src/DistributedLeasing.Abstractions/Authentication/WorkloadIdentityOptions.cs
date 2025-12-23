namespace DistributedLeasing.Abstractions.Authentication
{
    /// <summary>
    /// Configuration options for Workload Identity authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Workload Identity enables Kubernetes workloads and GitHub Actions to authenticate to Azure using federated credentials.
    /// </para>
    /// <para>
    /// <strong>Typical usage (environment variables set automatically by AKS or GitHub Actions):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "WorkloadIdentity"
    /// }
    /// </code>
    /// <para>
    /// <strong>Override environment variables if needed:</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "WorkloadIdentity",
    ///   "WorkloadIdentity": {
    ///     "TenantId": "87654321-4321-4321-4321-210987654321",
    ///     "ClientId": "abcdef12-abcd-abcd-abcd-abcdef123456",
    ///     "TokenFilePath": "/var/run/secrets/azure/tokens/azure-identity-token"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class WorkloadIdentityOptions
    {
        /// <summary>
        /// Gets or sets the Azure AD tenant ID.
        /// </summary>
        /// <value>
        /// The tenant ID, or <c>null</c> to use the AZURE_TENANT_ID environment variable.
        /// </value>
        /// <remarks>
        /// If not specified, the library will read from the AZURE_TENANT_ID environment variable.
        /// This is typically set automatically by AKS or GitHub Actions.
        /// </remarks>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the application (client) ID.
        /// </summary>
        /// <value>
        /// The client ID, or <c>null</c> to use the AZURE_CLIENT_ID environment variable.
        /// </value>
        /// <remarks>
        /// If not specified, the library will read from the AZURE_CLIENT_ID environment variable.
        /// This is typically set automatically by AKS or GitHub Actions.
        /// </remarks>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the path to the OIDC token file.
        /// </summary>
        /// <value>
        /// The token file path, or <c>null</c> to use the AZURE_FEDERATED_TOKEN_FILE environment variable.
        /// </value>
        /// <remarks>
        /// If not specified, the library will read from the AZURE_FEDERATED_TOKEN_FILE environment variable.
        /// This is typically set automatically by AKS or GitHub Actions.
        /// </remarks>
        public string? TokenFilePath { get; set; }
    }
}
