#if NET5_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistributedLeasing.Abstractions.Observability;

/// <summary>
/// Health check implementation for distributed leasing providers.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the lease provider can successfully acquire and release leases.
/// It's designed to be used with ASP.NET Core health check middleware.
/// </para>
/// <para>
/// <strong>Integration Example (ASP.NET Core):</strong>
/// </para>
/// <code>
/// builder.Services.AddHealthChecks()
///     .AddCheck&lt;LeaseHealthCheck&gt;("distributed-leasing", tags: new[] { "leasing", "storage" });
/// </code>
/// </remarks>
public class LeaseHealthCheck : IHealthCheck
{
    private readonly ILeaseProvider _provider;
    private readonly ILogger<LeaseHealthCheck> _logger;
    private readonly string _healthCheckLeaseName;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeaseHealthCheck"/> class.
    /// </summary>
    /// <param name="provider">The lease provider to check.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="healthCheckLeaseName">Name of the lease to use for health checks. Default is "__healthcheck__".</param>
    /// <param name="timeout">Timeout for health check operations. Default is 5 seconds.</param>
    public LeaseHealthCheck(
        ILeaseProvider provider, 
        ILogger<LeaseHealthCheck>? logger = null,
        string? healthCheckLeaseName = null,
        TimeSpan? timeout = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? NullLogger<LeaseHealthCheck>.Instance;
        _healthCheckLeaseName = healthCheckLeaseName ?? "__healthcheck__";
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            // Try to acquire a health check lease
            var lease = await _provider.AcquireLeaseAsync(
                _healthCheckLeaseName,
                TimeSpan.FromSeconds(15),
                cts.Token).ConfigureAwait(false);

            if (lease == null)
            {
                // Lease already held - this is acceptable for health check
                _logger.LogDebug("Health check lease '{LeaseName}' is currently held, which is acceptable", 
                    _healthCheckLeaseName);
                
                return HealthCheckResult.Healthy(
                    "Lease provider is responsive (lease currently held)",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "provider", _provider.GetType().Name },
                        { "lease_name", _healthCheckLeaseName },
                        { "status", "held" }
                    });
            }

            // Successfully acquired, now release
            try
            {
                await lease.ReleaseAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception releaseEx)
            {
                _logger.LogWarning(releaseEx, 
                    "Failed to release health check lease '{LeaseName}', but acquisition succeeded", 
                    _healthCheckLeaseName);
                
                // Acquisition worked, release failure is degraded but not unhealthy
                return HealthCheckResult.Degraded(
                    "Lease provider acquisition succeeded but release failed",
                    releaseEx,
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "provider", _provider.GetType().Name },
                        { "lease_name", _healthCheckLeaseName },
                        { "lease_id", lease.LeaseId },
                        { "status", "acquired_release_failed" }
                    });
            }

            _logger.LogDebug("Health check for lease provider '{Provider}' succeeded", 
                _provider.GetType().Name);

            return HealthCheckResult.Healthy(
                "Lease provider is healthy",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "provider", _provider.GetType().Name },
                    { "lease_name", _healthCheckLeaseName },
                    { "lease_id", lease.LeaseId },
                    { "status", "acquired_and_released" }
                });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Health check for lease provider '{Provider}' timed out after {Timeout}ms",
                _provider.GetType().Name, _timeout.TotalMilliseconds);

            return HealthCheckResult.Degraded(
                $"Lease provider health check timed out after {_timeout.TotalMilliseconds}ms",
                data: new System.Collections.Generic.Dictionary<string, object>
                {
                    { "provider", _provider.GetType().Name },
                    { "lease_name", _healthCheckLeaseName },
                    { "timeout_ms", _timeout.TotalMilliseconds }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check for lease provider '{Provider}' failed",
                _provider.GetType().Name);

            return HealthCheckResult.Unhealthy(
                "Lease provider is unhealthy",
                ex,
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "provider", _provider.GetType().Name },
                    { "lease_name", _healthCheckLeaseName },
                    { "exception", ex.GetType().Name },
                    { "error", ex.Message }
                });
        }
    }
}
#endif
