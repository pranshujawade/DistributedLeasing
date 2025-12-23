namespace DistributedLeasing.Abstractions.Authentication
{
    /// <summary>
    /// Configuration options for Federated Identity Credential authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Federated credentials enable external identity providers to authenticate to Azure without managing secrets.
    /// </para>
    /// <para>
    /// <strong>Example configuration:</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "FederatedCredential",
    ///   "FederatedCredential": {
    ///     "TenantId": "87654321-4321-4321-4321-210987654321",
    ///     "ClientId": "abcdef12-abcd-abcd-abcd-abcdef123456",
    ///     "TokenFilePath": "/var/run/secrets/tokens/oidc-token"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class FederatedCredentialOptions
    {
        /// <summary>
        /// Gets or sets the Azure AD tenant ID.
        /// </summary>
        /// <value>
        /// The tenant ID (required when Mode is FederatedCredential).
        /// </value>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the application (client) ID.
        /// </summary>
        /// <value>
        /// The client ID (required when Mode is FederatedCredential).
        /// </value>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the path to the external OIDC token file.
        /// </summary>
        /// <value>
        /// The token file path (required when Mode is FederatedCredential).
        /// </value>
        /// <remarks>
        /// The file must contain a valid OIDC token from the external identity provider.
        /// The token is exchanged for an Azure AD token during authentication.
        /// </remarks>
        public string? TokenFilePath { get; set; }

        /// <summary>
        /// Gets or sets the authority URL for the external identity provider.
        /// </summary>
        /// <value>
        /// The authority URL, or <c>null</c> to use the default Azure AD authority.
        /// </value>
        /// <remarks>
        /// Typically not needed unless using a custom authority.
        /// Defaults to Azure AD public cloud authority.
        /// </remarks>
        public string? Authority { get; set; }
    }
}
