using DistributedLeasing.Azure.Blob;

namespace BasicLeaseAcquisition;

/// <summary>
/// Demonstrates basic distributed lease acquisition using Azure Blob Storage.
/// </summary>
/// <remarks>
/// This sample shows how to:
/// <list type="bullet">
/// <item>Configure a blob lease manager</item>
/// <item>Acquire a distributed lease</item>
/// <item>Use automatic lease renewal</item>
/// <item>Handle lease events</item>
/// <item>Perform exclusive operations</item>
/// <item>Properly release the lease</item>
/// </list>
/// 
/// To run this sample, you need:
/// <list type="number">
/// <item>An Azure Storage account</item>
/// <item>Either managed identity enabled or a connection string</item>
/// <item>The Storage Blob Data Contributor role (if using managed identity)</item>
/// </list>
/// </remarks>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Distributed Leasing - Basic Acquisition Sample ===\n");

        // Configuration: Update these values for your environment
        var storageAccountUri = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT_URI")
            ?? "https://yourstorageaccount.blob.core.windows.net";
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        var useManagedIdentity = string.IsNullOrEmpty(connectionString);

        Console.WriteLine($"Storage Account: {storageAccountUri}");
        Console.WriteLine($"Auth Method: {(useManagedIdentity ? "Managed Identity" : "Connection String")}\n");

        // Create lease manager options
        var options = new BlobLeaseProviderOptions
        {
            StorageAccountUri = new Uri(storageAccountUri),
            ConnectionString = connectionString,
            UseManagedIdentity = useManagedIdentity,
            ContainerName = "leases",
            CreateContainerIfNotExists = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(30),
            
            // Enable automatic renewal (recommended for production)
            AutoRenew = true,
            AutoRenewInterval = TimeSpan.FromSeconds(20), // Renew at 2/3 of lease duration
            AutoRenewMaxRetries = 3,
            AutoRenewRetryInterval = TimeSpan.FromSeconds(2)
        };

        // Create the lease manager
        var leaseManager = new BlobLeaseManager(options);
        var resourceName = "critical-resource";

        Console.WriteLine($"Attempting to acquire lease on '{resourceName}'...");

        try
        {
            // Try to acquire the lease (non-blocking)
            await using var lease = await leaseManager.TryAcquireAsync(resourceName);

            if (lease != null)
            {
                Console.WriteLine("✓ Lease acquired successfully!");
                Console.WriteLine($"  Lease ID: {lease.LeaseId}");
                Console.WriteLine($"  Acquired At: {lease.AcquiredAt:O}");
                Console.WriteLine($"  Expires At: {lease.ExpiresAt:O}");
                Console.WriteLine($"  Is Acquired: {lease.IsAcquired}");
                Console.WriteLine($"  Auto-Renew Enabled: {options.AutoRenew}\n");

                // Subscribe to lease events
                lease.LeaseRenewed += (sender, e) =>
                {
                    Console.WriteLine($"[EVENT] Lease renewed at {e.Timestamp:HH:mm:ss}");
                    Console.WriteLine($"        New expiration: {e.NewExpiration:HH:mm:ss}");
                };

                lease.LeaseRenewalFailed += (sender, e) =>
                {
                    Console.WriteLine($"[EVENT] Lease renewal failed (Attempt {e.AttemptNumber})");
                    Console.WriteLine($"        Reason: {e.Exception.Message}");
                    Console.WriteLine($"        Will retry: {e.WillRetry}");
                };

                lease.LeaseLost += (sender, e) =>
                {
                    Console.WriteLine($"[EVENT] Lease lost at {e.Timestamp:HH:mm:ss}");
                    Console.WriteLine($"        Reason: {e.Reason}");
                };

                // Perform exclusive operations
                Console.WriteLine("Performing exclusive operation...");
                Console.WriteLine("(Notice that the lease is automatically renewed in the background)\n");
                await SimulateWork();

                // The lease has been automatically renewed during the work
                Console.WriteLine($"\n✓ Work completed. Current expiration: {lease.ExpiresAt:HH:mm:ss}");
                Console.WriteLine($"  Renewal count: {lease.RenewalCount}");

                Console.WriteLine("\nReleasing lease explicitly...");
                await lease.ReleaseAsync();
                Console.WriteLine("✓ Lease released successfully (auto-renewal stopped)");
            }
            else
            {
                Console.WriteLine("✗ Lease is currently held by another instance");
                Console.WriteLine("  This is normal when multiple instances compete for the same resource.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine("\n=== Sample Complete ===");
    }

    /// <summary>
    /// Simulates exclusive work being performed while holding the lease.
    /// </summary>
    static async Task SimulateWork()
    {
        for (int i = 1; i <= 5; i++)
        {
            Console.WriteLine($"  Work step {i}/5...");
            await Task.Delay(5000); // Simulate 5 seconds of work (total 25 seconds)
        }
        Console.WriteLine("  Work complete!");
    }
}
