using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Azure.Cosmos;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CosmosLeaseSample;

/// <summary>
/// Distributed Lock Demo - Demonstrates lock competition between multiple instances using Cosmos DB.
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
                
                // Register the Cosmos Lease Manager using proper configuration binding
                services.AddCosmosLeaseManager(options =>
                {
                    // Bind the entire CosmosLeasing section to the options object
                    configuration.GetSection("CosmosLeasing").Bind(options);
                    
                    // Add metadata to the options
                    foreach (var kvp in metadata)
                    {
                        options.Metadata[kvp.Key] = kvp.Value;
                    }
                });
                
                // Get configuration for metadata inspector
                var connectionString = configuration["CosmosLeasing:ConnectionString"];
                var accountEndpoint = configuration["CosmosLeasing:AccountEndpoint"];
                var databaseName = configuration["CosmosLeasing:DatabaseName"] ?? "DistributedLeasing";
                var containerName = configuration["CosmosLeasing:ContainerName"] ?? "Leases";
                
                if (!string.IsNullOrEmpty(connectionString) || !string.IsNullOrEmpty(accountEndpoint))
                {
                    var accountName = ExtractAccountName(connectionString, accountEndpoint);
                    
                    // Register metadata inspector
                    services.AddSingleton(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<CosmosMetadataInspector>>();
                        var leaseManager = sp.GetRequiredService<ILeaseManager>();
                        
                        // Access the provider's container
                        CosmosClient? cosmosClient = null;
                        if (!string.IsNullOrEmpty(connectionString))
                        {
                            cosmosClient = new CosmosClient(connectionString);
                        }
                        else if (!string.IsNullOrEmpty(accountEndpoint))
                        {
                            cosmosClient = new CosmosClient(accountEndpoint, new Azure.Identity.DefaultAzureCredential());
                        }
                        
                        var container = cosmosClient?.GetContainer(databaseName, containerName);
                        return new CosmosMetadataInspector(container!, logger, containerName, databaseName, accountName);
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
                    var metadataInspector = sp.GetService<CosmosMetadataInspector>();
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
                                                     ex.Message.Contains("Invalid URI"))
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
            Console.WriteLine("     ./setup-resources.sh");
            Console.WriteLine();
            Console.WriteLine("  2. Run with interactive configuration:");
            Console.WriteLine("     dotnet run --configure");
            Console.WriteLine();
            Console.WriteLine("  3. Manually create appsettings.Local.json with:");
            Console.WriteLine("     - AccountEndpoint: https://YOUR_ACCOUNT.documents.azure.com:443/");
            Console.WriteLine("     - ConnectionString: AccountEndpoint=https://...");
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

    private static string ExtractAccountName(string? connectionString, string? accountEndpoint)
    {
        if (!string.IsNullOrEmpty(accountEndpoint))
        {
            try
            {
                var uri = new Uri(accountEndpoint);
                return uri.Host.Split('.')[0];
            }
            catch
            {
                return "unknown";
            }
        }

        if (!string.IsNullOrEmpty(connectionString))
        {
            // Extract AccountEndpoint from connection string
            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.Trim().StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
                {
                    var endpoint = part.Substring(part.IndexOf('=') + 1).Trim();
                    try
                    {
                        var uri = new Uri(endpoint);
                        return uri.Host.Split('.')[0];
                    }
                    catch
                    {
                        return "unknown";
                    }
                }
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
