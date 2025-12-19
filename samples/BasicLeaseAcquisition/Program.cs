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
            AutoRenew = false // Manual renewal for demonstration
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
                Console.WriteLine($"  Is Acquired: {lease.IsAcquired}\n");

                // Perform exclusive operations
                Console.WriteLine("Performing exclusive operation...");
                await SimulateWork();

                // Demonstrate manual renewal
                Console.WriteLine("\nRenewing lease...");
                await lease.RenewAsync();
                Console.WriteLine($"✓ Lease renewed. New expiration: {lease.ExpiresAt:O}");

                // More work
                Console.WriteLine("\nPerforming more exclusive work...");
                await SimulateWork();

                Console.WriteLine("\nReleasing lease explicitly...");
                await lease.ReleaseAsync();
                Console.WriteLine("✓ Lease released successfully");
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
        for (int i = 1; i <= 3; i++)
        {
            Console.WriteLine($"  Work step {i}/3...");
            await Task.Delay(2000); // Simulate 2 seconds of work
        }
        Console.WriteLine("  Work complete!");
    }
}
