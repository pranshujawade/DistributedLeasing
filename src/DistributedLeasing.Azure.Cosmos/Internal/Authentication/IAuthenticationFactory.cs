using Azure.Core;

namespace DistributedLeasing.Azure.Cosmos.Internal.Authentication
{
    /// <summary>
    /// Factory for creating Azure credentials automatically from configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface is internal and used by provider implementations to create credentials.
    /// Users do not interact with this interface directly - all authentication is configured via appsettings.json.
    /// </para>
    /// <para>
    /// The factory reads the authentication configuration and automatically creates the appropriate
    /// <see cref="TokenCredential"/> instance, handling all token acquisition and refresh.
    /// </para>
    /// </remarks>
    internal interface IAuthenticationFactory
    {
        /// <summary>
        /// Creates a TokenCredential based on the authentication configuration.
        /// </summary>
        /// <param name="options">The authentication configuration from appsettings.json.</param>
        /// <returns>
        /// A configured <see cref="TokenCredential"/> that can be used to authenticate Azure SDK clients.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configuration is invalid or the credential cannot be created.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when required configuration properties are missing for the selected Mode.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method validates the configuration and creates the appropriate credential type
        /// based on the Mode setting. The returned credential handles all token acquisition and refresh automatically.
        /// </para>
        /// <para>
        /// Credentials are cached per configuration to avoid recreating them unnecessarily.
        /// </para>
        /// </remarks>
        TokenCredential CreateCredential(AuthenticationOptions options);

        /// <summary>
        /// Validates the authentication configuration without creating a credential.
        /// </summary>
        /// <param name="options">The authentication configuration to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configuration is invalid.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when required configuration properties are missing.
        /// </exception>
        /// <remarks>
        /// This method performs all configuration validation that would occur during credential creation,
        /// but does not actually create the credential. Useful for validating configuration early during startup.
        /// </remarks>
        void ValidateConfiguration(AuthenticationOptions options);
    }
}
