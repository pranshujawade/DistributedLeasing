using System;

namespace DistributedLeasing.Abstractions.Exceptions
{
    /// <summary>
    /// Base exception for all lease-related errors.
    /// </summary>
    /// <remarks>
    /// This is the base class for all exceptions thrown by the DistributedLeasing library.
    /// Catch this exception type to handle all lease-related errors generically.
    /// </remarks>
    public class LeaseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseException"/> class.
        /// </summary>
        public LeaseException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public LeaseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public LeaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Gets or sets the name of the lease associated with this exception.
        /// </summary>
        public string? LeaseName { get; set; }

        /// <summary>
        /// Gets or sets the lease ID associated with this exception.
        /// </summary>
        public string? LeaseId { get; set; }
    }

    /// <summary>
    /// Exception thrown when a lease cannot be acquired.
    /// </summary>
    /// <remarks>
    /// This exception indicates an unexpected failure during lease acquisition.
    /// Normal competition for a lease (where another instance currently holds it)
    /// is indicated by <see cref="ILeaseManager.TryAcquireAsync"/> returning null,
    /// not by throwing this exception.
    /// </remarks>
    public class LeaseAcquisitionException : LeaseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseAcquisitionException"/> class.
        /// </summary>
        public LeaseAcquisitionException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseAcquisitionException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public LeaseAcquisitionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseAcquisitionException"/> class
        /// with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public LeaseAcquisitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a lease renewal fails.
    /// </summary>
    /// <remarks>
    /// This exception typically indicates that the lease has expired or been acquired by
    /// another instance. When this exception is thrown, the lease should be considered lost
    /// and any operations depending on it should cease immediately.
    /// </remarks>
    public class LeaseRenewalException : LeaseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseRenewalException"/> class.
        /// </summary>
        public LeaseRenewalException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseRenewalException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public LeaseRenewalException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseRenewalException"/> class
        /// with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public LeaseRenewalException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when multiple instances attempt to acquire the same lease concurrently.
    /// </summary>
    /// <remarks>
    /// This exception indicates normal competition for a lease and can be handled by retrying
    /// the acquisition after a delay.
    /// </remarks>
    public class LeaseConflictException : LeaseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseConflictException"/> class.
        /// </summary>
        public LeaseConflictException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseConflictException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public LeaseConflictException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseConflictException"/> class
        /// with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public LeaseConflictException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a lease is lost unexpectedly.
    /// </summary>
    /// <remarks>
    /// This exception indicates that a lease has expired or been forcibly broken.
    /// Operations depending on the lease must cease immediately when this exception is encountered.
    /// </remarks>
    public class LeaseLostException : LeaseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException"/> class.
        /// </summary>
        public LeaseLostException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public LeaseLostException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException"/> class
        /// with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public LeaseLostException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when the underlying storage provider is unavailable.
    /// </summary>
    /// <remarks>
    /// This exception indicates that the lease storage service cannot be reached or is experiencing issues.
    /// Retry with exponential backoff or circuit breaker pattern is recommended.
    /// </remarks>
    public class ProviderUnavailableException : LeaseException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderUnavailableException"/> class.
        /// </summary>
        public ProviderUnavailableException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderUnavailableException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ProviderUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderUnavailableException"/> class
        /// with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ProviderUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Gets or sets the name of the provider that is unavailable.
        /// </summary>
        public string? ProviderName { get; set; }
    }
}
