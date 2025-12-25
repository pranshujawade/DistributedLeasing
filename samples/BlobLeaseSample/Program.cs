using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Events;
using DistributedLeasing.Abstractions.Exceptions;
using DistributedLeasing.Azure.Blob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlobLeaseSample;

/// <summary>
/// Sample application demonstrating Azure Blob Storage distributed leasing with automatic renewal.
/// </summary>
/// <remarks>
/// This sample shows:
/// - Dependency injection setup for ILeaseManager
/// - Configuration binding from appsettings.json
/// - Lease acquisition and automatic renewal
/// - Event handling for lease lifecycle (renewed, renewal failed, lost)
/// - Graceful shutdown and lease release
/// </remarks>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Build the host with configuration and dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                    optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Register the Blob Lease Manager using configuration binding
                services.AddBlobLeaseManager(context.Configuration.GetSection("BlobLeasing"));
                
                // Register the hosted service that will use the lease manager
                services.AddHostedService<LeaseWorkerService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        // Run the application
        await host.RunAsync();
    }
}

/// <summary>
/// Background service that demonstrates lease acquisition, automatic renewal, and event handling.
/// </summary>
public class LeaseWorkerService : BackgroundService
{
    private readonly ILeaseManager _leaseManager;
    private readonly ILogger<LeaseWorkerService> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private ILease? _currentLease;

    public LeaseWorkerService(
        ILeaseManager leaseManager,
        ILogger<LeaseWorkerService> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _leaseManager = leaseManager ?? throw new ArgumentNullException(nameof(leaseManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Blob Lease Sample Application starting...");

        try
        {
            // Attempt to acquire a lease for a named resource
            _logger.LogInformation("Attempting to acquire lease for resource 'sample-resource'...");
            
            _currentLease = await _leaseManager.TryAcquireAsync(
                leaseName: "sample-resource",
                duration: null, // Use default duration from configuration
                cancellationToken: stoppingToken);

            if (_currentLease == null)
            {
                _logger.LogWarning("Failed to acquire lease. Another instance may be holding it.");
                _logger.LogInformation("The lease will be automatically retried when it becomes available.");
                
                // In a real application, you might want to retry or exit gracefully
                // For this sample, we'll wait a bit and try again
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                
                // Try using AcquireAsync which waits for the lease to become available
                _logger.LogInformation("Waiting to acquire lease (will block until available)...");
                _currentLease = await _leaseManager.AcquireAsync(
                    leaseName: "sample-resource",
                    timeout: TimeSpan.FromSeconds(60),
                    cancellationToken: stoppingToken);
            }

            _logger.LogInformation(
                "Successfully acquired lease! LeaseId: {LeaseId}, AcquiredAt: {AcquiredAt}, ExpiresAt: {ExpiresAt}",
                _currentLease.LeaseId,
                _currentLease.AcquiredAt,
                _currentLease.ExpiresAt);

            // Subscribe to lease lifecycle events
            SubscribeToLeaseEvents(_currentLease);

            // Simulate doing work while holding the lease
            _logger.LogInformation("Performing work while holding the lease...");
            _logger.LogInformation("The lease will be automatically renewed in the background.");
            _logger.LogInformation("Press Ctrl+C to stop and release the lease.");

            // Keep working until cancellation is requested
            var workDuration = TimeSpan.Zero;
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                workDuration += TimeSpan.FromSeconds(5);

                // Check if we still have the lease
                if (_currentLease.IsAcquired)
                {
                    _logger.LogInformation(
                        "Still holding lease after {Duration:hh\\:mm\\:ss}. Renewal count: {RenewalCount}",
                        workDuration,
                        _currentLease.RenewalCount);
                }
                else
                {
                    _logger.LogError("Lease was lost! Stopping work.");
                    break;
                }
            }
        }
        catch (LeaseException ex)
        {
            _logger.LogError(ex, "Lease operation failed: {Message}", ex.Message);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout waiting for lease: {Message}", ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Application is shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred: {Message}", ex.Message);
        }
        finally
        {
            // Clean up and release the lease
            if (_currentLease != null)
            {
                await ReleaseLeaseAsync(_currentLease);
            }

            _logger.LogInformation("Blob Lease Sample Application stopped.");
        }
    }

    /// <summary>
    /// Subscribe to lease lifecycle events for monitoring and logging.
    /// </summary>
    private void SubscribeToLeaseEvents(ILease lease)
    {
        // Event: Lease successfully renewed
        lease.LeaseRenewed += (sender, e) =>
        {
            _logger.LogInformation(
                "Lease renewed successfully! LeaseId: {LeaseId}, NewExpiration: {NewExpiration}, RenewalDuration: {RenewalDuration}",
                e.LeaseId,
                e.NewExpiration,
                e.RenewalDuration);
        };

        // Event: Lease renewal failed (but will retry)
        lease.LeaseRenewalFailed += (sender, e) =>
        {
            _logger.LogWarning(
                "Lease renewal failed! LeaseId: {LeaseId}, Error: {Error}, WillRetry: {WillRetry}, AttemptNumber: {AttemptNumber}",
                e.LeaseId,
                e.Exception?.Message ?? "Unknown error",
                e.WillRetry,
                e.AttemptNumber);
        };

        // Event: Lease definitively lost (cannot be renewed)
        lease.LeaseLost += (sender, e) =>
        {
            _logger.LogError(
                "Lease lost! LeaseId: {LeaseId}, Reason: {Reason}",
                e.LeaseId,
                e.Reason);

            // In a real application, you would stop any work that depends on having the lease
            _logger.LogWarning("Stopping application because lease was lost...");
            _applicationLifetime.StopApplication();
        };
    }

    /// <summary>
    /// Explicitly release the lease to make it immediately available to other instances.
    /// </summary>
    private async Task ReleaseLeaseAsync(ILease lease)
    {
        try
        {
            _logger.LogInformation("Releasing lease {LeaseId}...", lease.LeaseId);
            await lease.ReleaseAsync();
            _logger.LogInformation("Lease released successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release lease: {Message}", ex.Message);
        }
        finally
        {
            // Dispose the lease to clean up resources
            await lease.DisposeAsync();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stop requested. Releasing lease...");
        await base.StopAsync(cancellationToken);
    }
}
