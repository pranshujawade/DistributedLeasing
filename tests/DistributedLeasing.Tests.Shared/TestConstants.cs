namespace DistributedLeasing.Tests.Shared;

/// <summary>
/// Centralized constants for test values to ensure consistency across test suites.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Common lease duration values for testing.
    /// </summary>
    public static class LeaseDurations
    {
        /// <summary>Short lease duration for fast tests (15 seconds - Azure Blob minimum).</summary>
        public static readonly TimeSpan Short = TimeSpan.FromSeconds(15);
        
        /// <summary>Medium lease duration for standard tests (60 seconds).</summary>
        public static readonly TimeSpan Medium = TimeSpan.FromSeconds(60);
        
        /// <summary>Long lease duration for extended scenarios (120 seconds).</summary>
        public static readonly TimeSpan Long = TimeSpan.FromSeconds(120);
    }

    /// <summary>
    /// Timeout values for async test operations.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>Fast timeout for quick operations (1 second).</summary>
        public static readonly TimeSpan Fast = TimeSpan.FromSeconds(1);
        
        /// <summary>Standard timeout for normal operations (5 seconds).</summary>
        public static readonly TimeSpan Standard = TimeSpan.FromSeconds(5);
        
        /// <summary>Extended timeout for slow operations (30 seconds).</summary>
        public static readonly TimeSpan Extended = TimeSpan.FromSeconds(30);
        
        /// <summary>Integration test timeout (60 seconds).</summary>
        public static readonly TimeSpan Integration = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Retry interval values for testing.
    /// </summary>
    public static class RetryIntervals
    {
        /// <summary>Fast retry interval (500 milliseconds).</summary>
        public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(500);
        
        /// <summary>Standard retry interval (1 second).</summary>
        public static readonly TimeSpan Standard = TimeSpan.FromSeconds(1);
        
        /// <summary>Slow retry interval (5 seconds).</summary>
        public static readonly TimeSpan Slow = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Common lease names for different test scenarios.
    /// </summary>
    public static class LeaseNames
    {
        /// <summary>Standard test lease name.</summary>
        public const string Standard = "test-lease";
        
        /// <summary>Lease name for concurrency tests.</summary>
        public const string Concurrent = "concurrent-lease";
        
        /// <summary>Lease name for auto-renewal tests.</summary>
        public const string AutoRenewal = "auto-renewal-lease";
        
        /// <summary>Lease name for health check tests.</summary>
        public const string HealthCheck = "__healthcheck__";
    }

    /// <summary>
    /// Connection strings for development and testing.
    /// </summary>
    public static class ConnectionStrings
    {
        /// <summary>Azurite development storage connection string.</summary>
        public const string AzuriteDevStorage = "UseDevelopmentStorage=true";
        
        /// <summary>Local Redis connection string.</summary>
        public const string LocalRedis = "localhost:6379";
        
        /// <summary>Cosmos DB emulator connection string.</summary>
        public const string CosmosEmulator = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    }

    /// <summary>
    /// Safety threshold values for auto-renewal testing.
    /// </summary>
    public static class SafetyThresholds
    {
        /// <summary>Standard safety threshold (0.9 or 90%).</summary>
        public const double Standard = 0.9;
        
        /// <summary>Aggressive safety threshold (0.75 or 75%).</summary>
        public const double Aggressive = 0.75;
        
        /// <summary>Conservative safety threshold (0.95 or 95%).</summary>
        public const double Conservative = 0.95;
    }

    /// <summary>
    /// Maximum retry counts for testing.
    /// </summary>
    public static class MaxRetries
    {
        /// <summary>No retries.</summary>
        public const int None = 0;
        
        /// <summary>Few retries (3 attempts).</summary>
        public const int Few = 3;
        
        /// <summary>Many retries (10 attempts).</summary>
        public const int Many = 10;
    }

    /// <summary>
    /// Test delay values for timing-based scenarios.
    /// </summary>
    public static class Delays
    {
        /// <summary>Tiny delay (50 milliseconds).</summary>
        public static readonly TimeSpan Tiny = TimeSpan.FromMilliseconds(50);
        
        /// <summary>Small delay (100 milliseconds).</summary>
        public static readonly TimeSpan Small = TimeSpan.FromMilliseconds(100);
        
        /// <summary>Medium delay (500 milliseconds).</summary>
        public static readonly TimeSpan Medium = TimeSpan.FromMilliseconds(500);
        
        /// <summary>Large delay (1 second).</summary>
        public static readonly TimeSpan Large = TimeSpan.FromSeconds(1);
    }
}
