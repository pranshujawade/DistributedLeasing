using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Azure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RedisLeaseSample;

/// <summary>
/// Distributed Lock Demo - Demonstrates lock competition between multiple instances using Redis.
/// </summary>
/// <remarks>
/// This demo shows:
/// - Two instances competing for the same lock
/// - Only one winner executes critical work
/// - Loser fails gracefully
/// - Takeover when winner releases lock
/// 
/// Usage:
///   Instance 1: dotnet run --instance us-east-1 --region us-east
///   Instance 2: dotnet run --instance eu-west-1 --region eu-west
/// </remarks>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Check for --configure flag
        bool forceReconfigure = args.Contains("--configure", StringComparer.OrdinalIgnoreCase);
        
        // Check if configuration file exists or if reconfiguration requested
        if (forceReconfigure || !ConfigurationHelper.CheckLocalConfigurationExists())
        {
            var setupSuccess = await ConfigurationHelper.RunInteractiveSetup();
            if (!setupSuccess)
            {
                return 1;
            }
        }
        
        // Parse command-line arguments for instance identification
        var instanceId = GetArgument(args, "--instance") ?? $"instance-{Guid.NewGuid():N}";
        var region = GetArgument(args, "--region") ?? "unknown-region";

        // Build the host with configuration and dependency injection
        IHost host;
        try
        {
            host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                    optional: true, reloadOnChange: true);
                config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                
                // Build metadata dictionary with instance information
                var metadata = new Dictionary<string, string>
                {
                    { "instanceId", instanceId },
                    { "region", region },
                    { "hostname", Environment.MachineName },
                    { "startTime", DateTimeOffset.UtcNow.ToString("o") }
                };
                
                // Register the Redis Lease Manager using proper configuration binding
                services.AddRedisLeaseManager(options =>
                {
                    // Bind the entire RedisLeasing section to the options object
                    configuration.GetSection("RedisLeasing").Bind(options);
                    
                    // Add metadata to the options
                    foreach (var kvp in metadata)
                    {
                        options.Metadata[kvp.Key] = kvp.Value;
                    }
                });
                
                // Get configuration for metadata inspector
                var connectionString = configuration["RedisLeasing:ConnectionString"];
                var endpoint = configuration["RedisLeasing:Endpoint"];
                var keyPrefix = configuration["RedisLeasing:KeyPrefix"] ?? "lease:";
                var database = configuration.GetValue<int>("RedisLeasing:Database", 0);
                
                if (!string.IsNullOrEmpty(connectionString) || !string.IsNullOrEmpty(endpoint))
                {
                    var cacheName = ExtractCacheName(connectionString, endpoint);
                    
                    // Register metadata inspector (with connection if possible)
                    services.AddSingleton(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<RedisMetadataInspector>>();
                        
                        // Try to get connection from LeaseManager
                        IConnectionMultiplexer? connection = null;
                        try
                        {
                            // Create a connection for inspection purposes
                            if (!string.IsNullOrEmpty(connectionString))
                            {
                                connection = ConnectionMultiplexer.Connect(connectionString);
                            }
                        }
                        catch
                        {
                            // Connection optional for inspector
                        }
                        
                        return new RedisMetadataInspector(connection, logger, cacheName, database);
                    });
                }
                
                // Register instance information
                services.AddSingleton(new InstanceInfo(instanceId, region));
                
                // Register the distributed lock worker
                services.AddSingleton<DistributedLockWorker>(sp =>
                {
                    var leaseManager = sp.GetRequiredService<ILeaseManager>();
                    var logger = sp.GetRequiredService<ILogger<DistributedLockWorker>>();
                    var instanceInfo = sp.GetRequiredService<InstanceInfo>();
                    var metadataInspector = sp.GetService<RedisMetadataInspector>();
                    return new DistributedLockWorker(leaseManager, logger, instanceInfo.InstanceId, instanceInfo.Region, metadataInspector);
                });
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddColoredConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to convert configuration") || 
                                                     ex.Message.Contains("Invalid URI") ||
                                                     ex.Message.Contains("Authentication must be configured"))
        {
            Console.WriteLine();
            Console.WriteLine("=".PadRight(67, '='));
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  CONFIGURATION ERROR");
            Console.ResetColor();
            Console.WriteLine("=".PadRight(67, '='));
            Console.WriteLine();
            Console.WriteLine("The application could not start due to missing or invalid configuration.");
            Console.WriteLine();
            Console.WriteLine("Problem: appsettings.Local.json not found or contains placeholders");
            Console.WriteLine();
            Console.WriteLine("Solution: Choose one of these options:");
            Console.WriteLine();
            Console.WriteLine("  1. Run automatic setup:");
            Console.WriteLine("     cd ../../scripts && ./setup-resources.sh --project redis");
            Console.WriteLine();
            Console.WriteLine("  2. Run with interactive configuration:");
            Console.WriteLine("     dotnet run --configure");
            Console.WriteLine();
            Console.WriteLine("  3. Manually create appsettings.Local.json with:");
            Console.WriteLine("     - Endpoint: mycache.redis.cache.windows.net:6380");
            Console.WriteLine("     - ConnectionString: mycache.redis.cache.windows.net:6380,password=...");
            Console.WriteLine();
            Console.WriteLine("For more help, see: README.md");
            Console.WriteLine();
            Console.WriteLine("=".PadRight(67, '='));
            Console.WriteLine();
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Failed to start application: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
            return 1;
        }

        // Get the worker and execute the lock demo
        var worker = host.Services.GetRequiredService<DistributedLockWorker>();
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        try
        {
            await worker.TryExecuteWithLockAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutdown requested.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        return 0;
    }

    private static string ExtractCacheName(string? connectionString, string? endpoint)
    {
        if (!string.IsNullOrEmpty(endpoint))
        {
            try
            {
                return endpoint.Split(':')[0].Split('.')[0];
            }
            catch
            {
                return "unknown";
            }
        }

        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                return connectionString.Split(',')[0].Split(':')[0].Split('.')[0];
            }
            catch
            {
                return "unknown";
            }
        }
        
        return "unknown";
    }

    private static string? GetArgument(string[] args, string argName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}

/// <summary>
/// Instance information for identification and metadata.
/// </summary>
public record InstanceInfo(string InstanceId, string Region);
