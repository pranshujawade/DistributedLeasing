namespace DistributedLeasing.Azure.Redis.Internal.Authentication
{
    /// <summary>
    /// Configuration options for Service Principal authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Service Principal authentication allows applications to authenticate using an Azure AD application identity.
    /// </para>
    /// <para>
    /// <strong>Certificate-based authentication (RECOMMENDED):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "ServicePrincipal",
    ///   "ServicePrincipal": {
    ///     "TenantId": "87654321-4321-4321-4321-210987654321",
    ///     "ClientId": "abcdef12-abcd-abcd-abcd-abcdef123456",
    ///     "CertificatePath": "/var/secrets/app-cert.pem"
    ///   }
    /// }
    /// </code>
    /// <para>
    /// <strong>Client secret authentication (NOT RECOMMENDED for production):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "ServicePrincipal",
    ///   "ServicePrincipal": {
    ///     "TenantId": "87654321-4321-4321-4321-210987654321",
    ///     "ClientId": "abcdef12-abcd-abcd-abcd-abcdef123456",
    ///     "ClientSecret": "your-secret-from-keyvault"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class ServicePrincipalOptions
    {
        /// <summary>
        /// Gets or sets the Azure AD tenant ID.
        /// </summary>
        /// <value>
        /// The tenant ID (required when Mode is ServicePrincipal).
        /// </value>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the application (client) ID.
        /// </summary>
        /// <value>
        /// The client ID (required when Mode is ServicePrincipal).
        /// </value>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the path to the certificate file (.pem or .pfx).
        /// </summary>
        /// <value>
        /// The certificate file path, or <c>null</c> if using client secret.
        /// </value>
        /// <remarks>
        /// <para>
        /// Certificate-based authentication is strongly recommended over client secrets.
        /// </para>
        /// <para>
        /// Supported formats: .pem (PEM-encoded certificate) or .pfx (PKCS#12).
        /// </para>
        /// <para>
        /// If both CertificatePath and ClientSecret are provided, the certificate takes precedence.
        /// </para>
        /// </remarks>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the password for encrypted certificate files.
        /// </summary>
        /// <value>
        /// The certificate password, or <c>null</c> for unencrypted certificates.
        /// </value>
        /// <remarks>
        /// Only required if the certificate file (.pfx) is password-protected.
        /// </remarks>
        public string? CertificatePassword { get; set; }

        /// <summary>
        /// Gets or sets the client secret.
        /// </summary>
        /// <value>
        /// The client secret, or <c>null</c> if using certificate.
        /// </value>
        /// <remarks>
        /// <para>
        /// <strong>WARNING:</strong> Client secrets are not recommended for production use.
        /// Use certificate-based authentication instead.
        /// </para>
        /// <para>
        /// The library will log a security warning if client secret is used.
        /// </para>
        /// <para>
        /// Store secrets in Azure Key Vault and reference them in configuration, never hardcode.
        /// </para>
        /// </remarks>
        public string? ClientSecret { get; set; }
    }
}
