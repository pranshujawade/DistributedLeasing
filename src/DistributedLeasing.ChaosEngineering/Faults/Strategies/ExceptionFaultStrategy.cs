using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Faults.Strategies
{
    /// <summary>
    /// Fault strategy that throws configurable exceptions to simulate failures.
    /// </summary>
    /// <remarks>
    /// This strategy is useful for testing error handling, retry logic, and
    /// recovery mechanisms in distributed leasing scenarios.
    /// </remarks>
    public class ExceptionFaultStrategy : FaultStrategyBase
    {
        private readonly Type _exceptionType;
        private readonly string _message;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionFaultStrategy"/> class.
        /// </summary>
        /// <param name="exceptionType">The type of exception to throw. Must derive from <see cref="Exception"/>.</param>
        /// <param name="message">The exception message.</param>
        /// <exception cref="ArgumentNullException">Thrown when exceptionType is null.</exception>
        /// <exception cref="ArgumentException">Thrown when exceptionType does not derive from Exception.</exception>
        public ExceptionFaultStrategy(Type exceptionType, string message = "Chaos engineering fault injection")
        {
            if (exceptionType == null)
            {
                throw new ArgumentNullException(nameof(exceptionType));
            }

            if (!typeof(Exception).IsAssignableFrom(exceptionType))
            {
                throw new ArgumentException(
                    $"Type {exceptionType.Name} must derive from System.Exception",
                    nameof(exceptionType));
            }

            _exceptionType = exceptionType;
            _message = message ?? "Chaos engineering fault injection";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionFaultStrategy"/> class
        /// with a specific exception type.
        /// </summary>
        /// <typeparam name="TException">The type of exception to throw.</typeparam>
        /// <param name="message">The exception message.</param>
        public static ExceptionFaultStrategy Create<TException>(string message = "Chaos engineering fault injection")
            where TException : Exception
        {
            return new ExceptionFaultStrategy(typeof(TException), message);
        }

        /// <inheritdoc/>
        public override string Name => "Exception";

        /// <inheritdoc/>
        public override string Description => 
            $"Throws {_exceptionType.Name}: {_message}";

        /// <inheritdoc/>
        public override FaultSeverity Severity
        {
            get
            {
                // Categorize based on exception type
                var exceptionName = _exceptionType.Name;
                
                if (exceptionName.Contains("Timeout") || exceptionName.Contains("Canceled"))
                    return FaultSeverity.Medium;
                
                if (exceptionName.Contains("Unavailable") || exceptionName.Contains("Connection"))
                    return FaultSeverity.High;
                
                if (exceptionName.Contains("Critical") || exceptionName.Contains("Fatal"))
                    return FaultSeverity.Critical;
                
                return FaultSeverity.High; // Default for exceptions
            }
        }

        /// <inheritdoc/>
        public override Task ExecuteAsync(FaultContext context, CancellationToken cancellationToken = default)
        {
            ValidateContext(context);

            // Store exception info in context metadata for observability
            context.Metadata["ExceptionType"] = _exceptionType.Name;
            context.Metadata["ExceptionMessage"] = _message;

            // Create and throw the exception
            var exception = CreateException();
            throw exception;
        }

        /// <summary>
        /// Creates an instance of the configured exception type.
        /// </summary>
        /// <returns>An exception instance.</returns>
        private Exception CreateException()
        {
            try
            {
                // Try to create with message constructor
                var messageConstructor = _exceptionType.GetConstructor(new[] { typeof(string) });
                if (messageConstructor != null)
                {
                    return (Exception)messageConstructor.Invoke(new object[] { _message });
                }

                // Try to create with default constructor and set message via property
                var defaultConstructor = _exceptionType.GetConstructor(Type.EmptyTypes);
                if (defaultConstructor != null)
                {
                    var exception = (Exception)defaultConstructor.Invoke(null);
                    // Note: Exception.Message is read-only, so we return the instance as-is
                    return exception;
                }

                // Fallback: create generic Exception with descriptive message
                return new Exception(
                    $"Failed to create {_exceptionType.Name} instance. Original message: {_message}");
            }
            catch (Exception ex)
            {
                // If exception creation fails, throw a generic exception with details
                throw new InvalidOperationException(
                    $"Failed to create exception of type {_exceptionType.Name}: {ex.Message}",
                    ex);
            }
        }
    }
}
