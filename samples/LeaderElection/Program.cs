using DistributedLeasing.Azure.Blob;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Exceptions;

namespace LeaderElection;

/// <summary>
/// Demonstrates distributed leader election using the DistributedLeasing library.
/// </summary>
/// <remarks>
/// This sample shows how multiple instances can compete for leadership using a distributed lease.
/// The instance that acquires the lease becomes the leader and performs leader-only operations.
/// Other instances become followers and continuously attempt to acquire leadership.
/// 
/// This sample demonstrates automatic lease renewal - the leader's lease is automatically renewed
/// in the background, eliminating the need for manual renewal logic.
/// 
/// To simulate multiple instances, run this program in multiple terminals simultaneously.
/// </remarks>
class Program
{
    private static readonly string InstanceId = Guid.NewGuid().ToString("N")[..8];
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Distributed Leader Election Sample ===\n");
        Console.WriteLine($"Instance ID: {InstanceId}");
        Console.WriteLine("Press Ctrl+C to exit\n");

        // Handle graceful shutdown
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            _isRunning = false;
            Console.WriteLine("\nShutdown requested...");
        };

        // Configuration
        var storageAccountUri = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT_URI")
            ?? "https://yourstorageaccount.blob.core.windows.net";
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        var useManagedIdentity = string.IsNullOrEmpty(connectionString);

        var options = new BlobLeaseProviderOptions
        {
            StorageAccountUri = new Uri(storageAccountUri),
            ConnectionString = connectionString,
            // Use new Authentication property instead of UseManagedIdentity
            Authentication = useManagedIdentity ? new DistributedLeasing.Authentication.AuthenticationOptions
            {
                Mode = DistributedLeasing.Authentication.AuthenticationModes.Auto
            } : null,
            ContainerName = "leader-election",
            CreateContainerIfNotExists = true,
            DefaultLeaseDuration = TimeSpan.FromSeconds(30),
            
            // Enable automatic renewal for leader lease
            AutoRenew = true,
            AutoRenewInterval = TimeSpan.FromSeconds(20), // Renew at 2/3 of lease duration
            AutoRenewMaxRetries = 3,
            AutoRenewRetryInterval = TimeSpan.FromSeconds(2)
        };

        var leaseManager = new BlobLeaseManager(options);
        var leaderLeaseName = "cluster-leader";

        Console.WriteLine($"Competing for leadership on '{leaderLeaseName}'...\n");

        while (_isRunning)
        {
            try
            {
                // Try to become the leader
                await using var lease = await leaseManager.TryAcquireAsync(
                    leaderLeaseName,
                    options.DefaultLeaseDuration);

                if (lease != null)
                {
                    // We are the leader!
                    await RunAsLeader(lease);
                }
                else
                {
                    // We are a follower
                    await RunAsFollower();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{InstanceId}] Error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        Console.WriteLine($"[{InstanceId}] Exiting gracefully");
    }

    /// <summary>
    /// Executes leader responsibilities.
    /// </summary>
    static async Task RunAsLeader(ILease lease)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{InstanceId}] *** I AM THE LEADER ***");
        Console.WriteLine($"[{InstanceId}] Auto-renewal is enabled - lease will be renewed automatically");
        Console.ResetColor();

        var iteration = 0;
        
        // Subscribe to lease events
        lease.LeaseRenewed += (sender, e) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"[{InstanceId}][LEADER] Lease auto-renewed at {e.Timestamp:HH:mm:ss} (Expiration: {e.NewExpiration:HH:mm:ss})");
            Console.ResetColor();
        };

        lease.LeaseRenewalFailed += (sender, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{InstanceId}][LEADER] Renewal failed (Attempt {e.AttemptNumber}): {e.Exception.Message}");
            Console.WriteLine($"[{InstanceId}][LEADER] Will retry: {e.WillRetry}");
            Console.ResetColor();
        };

        lease.LeaseLost += (sender, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{InstanceId}][LEADER] *** LEADERSHIP LOST ***");
            Console.WriteLine($"[{InstanceId}][LEADER] Reason: {e.Reason}");
            Console.ResetColor();
        };

        while (_isRunning && lease.IsAcquired)
        {
            try
            {
                iteration++;
                Console.WriteLine($"[{InstanceId}][LEADER] Iteration {iteration} - Performing leader duties...");

                // Simulate leader work
                await PerformLeaderWork(iteration);

                // No manual renewal needed - it's automatic!
                Console.WriteLine($"[{InstanceId}][LEADER] Current lease status: Expires at {lease.ExpiresAt:HH:mm:ss}, Renewals: {lease.RenewalCount}");

                // Sleep before next iteration
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{InstanceId}][LEADER] Error: {ex.Message}");
                break;
            }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{InstanceId}] *** STEPPING DOWN AS LEADER ***");
        Console.ResetColor();
    }

    /// <summary>
    /// Executes follower responsibilities.
    /// </summary>
    static async Task RunAsFollower()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{InstanceId}] I am a follower. Waiting for leadership opportunity...");
        Console.ResetColor();

        // Simulate follower work
        await PerformFollowerWork();

        // Wait before trying to acquire leadership again
        await Task.Delay(TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Simulates work that only the leader should perform.
    /// </summary>
    static async Task PerformLeaderWork(int iteration)
    {
        // Examples of leader-only operations:
        // - Coordinating distributed tasks
        // - Processing singleton jobs
        // - Managing cluster state
        // - Scheduling background work

        Console.WriteLine($"  → Processing cluster-wide task #{iteration}");
        await Task.Delay(1000);
        Console.WriteLine($"  → Coordinating distributed operations");
        await Task.Delay(1000);
        Console.WriteLine($"  → Leader duties complete for this iteration");
    }

    /// <summary>
    /// Simulates work that followers perform.
    /// </summary>
    static async Task PerformFollowerWork()
    {
        // Examples of follower operations:
        // - Processing local tasks
        // - Monitoring health
        // - Maintaining standby readiness

        Console.WriteLine($"  → Processing local tasks");
        await Task.Delay(1000);
        Console.WriteLine($"  → Monitoring system health");
        await Task.Delay(1000);
        Console.WriteLine($"  → Standing by for leadership");
    }
}
