namespace DistributedLeasing.Azure.Redis.Internal.Authentication
{
    /// <summary>
    /// Configuration options for Managed Identity authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Azure Managed Identity provides automatic authentication for Azure resources without managing credentials.
    /// </para>
    /// <para>
    /// <strong>System-Assigned Managed Identity (recommended for single-resource scenarios):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "ManagedIdentity"
    /// }
    /// </code>
    /// <para>
    /// <strong>User-Assigned Managed Identity (for scenarios where multiple resources share an identity):</strong>
    /// </para>
    /// <code>
    /// "Authentication": {
    ///   "Mode": "ManagedIdentity",
    ///   "ManagedIdentity": {
    ///     "ClientId": "12345678-1234-1234-1234-123456789012"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class ManagedIdentityOptions
    {
        /// <summary>
        /// Gets or sets the client ID of the user-assigned managed identity.
        /// </summary>
        /// <value>
        /// The client ID (GUID) of the user-assigned managed identity, or <c>null</c> to use system-assigned identity.
        /// </value>
        /// <remarks>
        /// <para>
        /// Leave this null or empty to use system-assigned managed identity.
        /// </para>
        /// <para>
        /// Provide a client ID to use a specific user-assigned managed identity.
        /// The client ID can be found in the Azure portal under the managed identity's properties.
        /// </para>
        /// </remarks>
        public string? ClientId { get; set; }
    }
}
