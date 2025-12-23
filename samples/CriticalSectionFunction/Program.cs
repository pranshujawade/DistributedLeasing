using DistributedLeasing.Azure.Blob;
using DistributedLeasing.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CriticalSectionFunction;

/// <summary>
/// Program entry point for the Azure Function that demonstrates distributed leasing
/// for critical section protection.
/// </summary>
public class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureAppConfiguration((context, config) =>
            {
                config
                    .SetBasePath(context.HostingEnvironment.ContentRootPath)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .AddUserSecrets<Program>(optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                // Register distributed leasing with auto-renewal
                services.AddBlobLeaseManager(options =>
                {
                    var config = context.Configuration;
                    
                    // Get configuration from appsettings or environment variables
                    var storageAccountUri = config["DistributedLeasing:StorageAccountUri"];
                    var connectionString = config["DistributedLeasing:ConnectionString"];
                    var useManagedIdentity = config.GetValue<bool>("DistributedLeasing:UseManagedIdentity", true);

                    if (!string.IsNullOrEmpty(storageAccountUri))
                    {
                        options.StorageAccountUri = new Uri(storageAccountUri);
                    }
                    
                    options.ConnectionString = connectionString;
                    // Use new Authentication property instead of UseManagedIdentity
                    if (useManagedIdentity && !string.IsNullOrEmpty(storageAccountUri))
                    {
                        options.Authentication = new DistributedLeasing.Authentication.AuthenticationOptions
                        {
                            Mode = DistributedLeasing.Authentication.AuthenticationModes.Auto
                        };
                    }
                    options.ContainerName = config["DistributedLeasing:ContainerName"] ?? "leases";
                    options.CreateContainerIfNotExists = config.GetValue("DistributedLeasing:CreateContainerIfNotExists", true);
                    
                    // Configure auto-renewal for long-running operations
                    options.AutoRenew = config.GetValue("DistributedLeasing:AutoRenew", true);
                    options.DefaultLeaseDuration = TimeSpan.Parse(
                        config["DistributedLeasing:DefaultLeaseDuration"] ?? "00:00:30");
                    options.AutoRenewInterval = TimeSpan.Parse(
                        config["DistributedLeasing:AutoRenewInterval"] ?? "00:00:20");
                    options.AutoRenewMaxRetries = config.GetValue("DistributedLeasing:AutoRenewMaxRetries", 3);
                });

                // Register the inventory service
                services.AddSingleton<IInventoryService, InventoryService>();
            })
            .Build();

        host.Run();
    }
}
