using DistributedLeasing.Core;
using DistributedLeasing.Core.Events;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CriticalSectionFunction;

/// <summary>
/// Implementation of inventory service using distributed leasing for critical section protection.
/// </summary>
/// <remarks>
/// This service uses distributed leases to ensure that inventory operations are atomic
/// across multiple Azure Function instances. Each product has its own lease to allow
/// concurrent operations on different products while maintaining consistency.
/// </remarks>
public class InventoryService : IInventoryService
{
    private readonly ILeaseManager _leaseManager;
    private readonly ILogger<InventoryService> _logger;
    
    // In-memory storage (in production, use Azure Table Storage, Cosmos DB, or SQL Database)
    private static readonly ConcurrentDictionary<string, ProductInventory> _inventory = new();
    private static readonly ConcurrentDictionary<string, ReservationInternal> _reservations = new();

    public InventoryService(
        ILeaseManager leaseManager,
        ILogger<InventoryService> logger)
    {
        _leaseManager = leaseManager;
        _logger = logger;
        
        // Initialize sample data
        InitializeSampleData();
    }

    /// <inheritdoc/>
    public async Task<ReservationResult> ReserveInventoryAsync(
        string productId,
        int quantity,
        string customerId)
    {
        var leaseName = GetProductLeaseName(productId);
        var startTime = Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "Attempting to reserve {Quantity} units of product {ProductId} for customer {CustomerId}",
            quantity, productId, customerId);

        // Acquire lease for this specific product with auto-renewal enabled
        await using var lease = await _leaseManager.TryAcquireAsync(
            leaseName,
            duration: TimeSpan.FromSeconds(30));

        if (lease == null)
        {
            _logger.LogWarning(
                "Could not acquire lease for product {ProductId}. Another operation in progress.",
                productId);
            
            var currentInventory = _inventory.GetOrAdd(productId, _ => new ProductInventory(productId, 0));
            
            return new ReservationResult(
                Success: false,
                ProductId: productId,
                RequestedQuantity: quantity,
                AvailableQuantity: currentInventory.AvailableQuantity,
                ReservationId: null,
                Message: "Another operation is in progress. Please retry.",
                AcquiredLease: false);
        }

        try
        {
            // Subscribe to lease events for monitoring
            lease.LeaseRenewed += OnLeaseRenewed;
            lease.LeaseRenewalFailed += OnLeaseRenewalFailed;
            lease.LeaseLost += OnLeaseLost;

            _logger.LogInformation(
                "? Acquired lease {LeaseId} for product {ProductId}",
                lease.LeaseId, productId);

            // Critical section: Update inventory atomically
            var inventory = _inventory.GetOrAdd(productId, _ => new ProductInventory(productId, 0));

            if (inventory.AvailableQuantity < quantity)
            {
                _logger.LogWarning(
                    "Insufficient inventory for product {ProductId}. Available: {Available}, Requested: {Requested}",
                    productId, inventory.AvailableQuantity, quantity);

                return new ReservationResult(
                    Success: false,
                    ProductId: productId,
                    RequestedQuantity: quantity,
                    AvailableQuantity: inventory.AvailableQuantity,
                    ReservationId: null,
                    Message: $"Insufficient inventory. Available: {inventory.AvailableQuantity}",
                    AcquiredLease: true);
            }

            // Simulate some processing time (database operations, validation, etc.)
            await Task.Delay(2000); // The lease will auto-renew during this time if needed

            // Create reservation
            var reservationId = Guid.NewGuid().ToString();
            var reservation = new ReservationInternal
            {
                ReservationId = reservationId,
                CustomerId = customerId,
                Quantity = quantity,
                ReservedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                ProductId = productId
            };

            _reservations[reservationId] = reservation;

            // Update inventory
            inventory.AvailableQuantity -= quantity;
            inventory.ReservedQuantity += quantity;

            var elapsed = Stopwatch.GetElapsedTime(startTime);
            _logger.LogInformation(
                "? Successfully reserved {Quantity} units of product {ProductId}. " +
                "Reservation ID: {ReservationId}. Operation took {Duration}ms. Renewal count: {RenewalCount}",
                quantity, productId, reservationId, elapsed.TotalMilliseconds, lease.RenewalCount);

            return new ReservationResult(
                Success: true,
                ProductId: productId,
                RequestedQuantity: quantity,
                AvailableQuantity: inventory.AvailableQuantity,
                ReservationId: reservationId,
                Message: $"Successfully reserved {quantity} units",
                AcquiredLease: true);
        }
        finally
        {
            // Lease is automatically released on dispose
            _logger.LogInformation(
                "Releasing lease {LeaseId} for product {ProductId}",
                lease.LeaseId, productId);
        }
    }

    /// <inheritdoc/>
    public async Task<ReleaseResult> ReleaseInventoryAsync(
        string productId,
        int quantity,
        string customerId)
    {
        var leaseName = GetProductLeaseName(productId);

        _logger.LogInformation(
            "Attempting to release {Quantity} units of product {ProductId} for customer {CustomerId}",
            quantity, productId, customerId);

        // Acquire lease for atomic operation
        await using var lease = await _leaseManager.TryAcquireAsync(
            leaseName,
            duration: TimeSpan.FromSeconds(15));

        if (lease == null)
        {
            return new ReleaseResult(
                Success: false,
                ProductId: productId,
                ReleasedQuantity: 0,
                NewAvailableQuantity: 0,
                Message: "Could not acquire lease. Please retry.");
        }

        try
        {
            // Subscribe to events
            lease.LeaseRenewed += OnLeaseRenewed;

            var inventory = _inventory.GetOrAdd(productId, _ => new ProductInventory(productId, 0));

            // Find and release reservation
            var customerReservations = _reservations.Values
                .Where(r => r.ProductId == productId && r.CustomerId == customerId)
                .ToList();

            if (!customerReservations.Any())
            {
                return new ReleaseResult(
                    Success: false,
                    ProductId: productId,
                    ReleasedQuantity: 0,
                    NewAvailableQuantity: inventory.AvailableQuantity,
                    Message: "No active reservations found for this customer");
            }

            var quantityToRelease = Math.Min(quantity, customerReservations.Sum(r => r.Quantity));

            // Update inventory
            inventory.AvailableQuantity += quantityToRelease;
            inventory.ReservedQuantity -= quantityToRelease;

            // Remove or update reservations
            var remaining = quantityToRelease;
            foreach (var reservation in customerReservations)
            {
                if (remaining <= 0) break;

                if (reservation.Quantity <= remaining)
                {
                    _reservations.TryRemove(reservation.ReservationId, out _);
                    remaining -= reservation.Quantity;
                }
                else
                {
                    var updated = new ReservationInternal
                    {
                        ReservationId = reservation.ReservationId,
                        CustomerId = reservation.CustomerId,
                        Quantity = reservation.Quantity - remaining,
                        ReservedAt = reservation.ReservedAt,
                        ExpiresAt = reservation.ExpiresAt,
                        ProductId = reservation.ProductId
                    };
                    _reservations[reservation.ReservationId] = updated;
                    remaining = 0;
                }
            }

            _logger.LogInformation(
                "? Released {Quantity} units of product {ProductId}",
                quantityToRelease, productId);

            return new ReleaseResult(
                Success: true,
                ProductId: productId,
                ReleasedQuantity: quantityToRelease,
                NewAvailableQuantity: inventory.AvailableQuantity,
                Message: $"Successfully released {quantityToRelease} units");
        }
        finally
        {
            _logger.LogInformation("Releasing lease for product {ProductId}", productId);
        }
    }

    /// <inheritdoc/>
    public Task<InventoryStatus> GetInventoryAsync(string productId)
    {
        var inventory = _inventory.GetOrAdd(productId, _ => new ProductInventory(productId, 0));

        var activeReservations = _reservations.Values
            .Where(r => r.ProductId == productId && r.ExpiresAt > DateTimeOffset.UtcNow)
            .Select(r => r.ToReservation())
            .ToList();

        return Task.FromResult(new InventoryStatus(
            ProductId: productId,
            TotalQuantity: inventory.TotalQuantity,
            AvailableQuantity: inventory.AvailableQuantity,
            ReservedQuantity: inventory.ReservedQuantity,
            ActiveReservations: activeReservations));
    }

    /// <inheritdoc/>
    public async Task<ReconciliationResult> ReconcileInventoryAsync()
    {
        var reconciliationLease = "inventory-reconciliation-job";
        var startTime = Stopwatch.GetTimestamp();

        _logger.LogInformation("Starting inventory reconciliation...");

        // Try to acquire the reconciliation lease (only one instance should do this)
        await using var lease = await _leaseManager.TryAcquireAsync(
            reconciliationLease,
            duration: TimeSpan.FromMinutes(5)); // Longer duration for reconciliation

        if (lease == null)
        {
            _logger.LogInformation(
                "Another instance is performing reconciliation. Skipping.");
            
            return new ReconciliationResult(
                WasProcessed: false,
                ProductsReconciled: 0,
                ExpiredReservationsReleased: 0,
                Duration: TimeSpan.Zero,
                LeaseId: null);
        }

        try
        {
            // Subscribe to events
            lease.LeaseRenewed += OnLeaseRenewed;
            lease.LeaseRenewalFailed += OnLeaseRenewalFailed;
            lease.LeaseLost += OnLeaseLost;

            _logger.LogInformation(
                "? Acquired reconciliation lease {LeaseId}. Starting reconciliation...",
                lease.LeaseId);

            var productsReconciled = 0;
            var expiredReservationsReleased = 0;

            // Process each product
            foreach (var product in _inventory.Values)
            {
                // Simulate reconciliation work
                await Task.Delay(1000); // Auto-renewal will keep the lease alive

                var expiredReservations = _reservations.Values
                    .Where(r => r.ProductId == product.ProductId && r.ExpiresAt <= DateTimeOffset.UtcNow)
                    .ToList();

                foreach (var expired in expiredReservations)
                {
                    if (_reservations.TryRemove(expired.ReservationId, out _))
                    {
                        product.AvailableQuantity += expired.Quantity;
                        product.ReservedQuantity -= expired.Quantity;
                        expiredReservationsReleased++;

                        _logger.LogInformation(
                            "Released expired reservation {ReservationId} for product {ProductId}: {Quantity} units",
                            expired.ReservationId, product.ProductId, expired.Quantity);
                    }
                }

                productsReconciled++;
            }

            var elapsed = Stopwatch.GetElapsedTime(startTime);
            
            _logger.LogInformation(
                "? Reconciliation completed. Products: {Products}, Expired reservations: {Expired}, " +
                "Duration: {Duration}s, Renewals: {Renewals}",
                productsReconciled, expiredReservationsReleased, elapsed.TotalSeconds, lease.RenewalCount);

            return new ReconciliationResult(
                WasProcessed: true,
                ProductsReconciled: productsReconciled,
                ExpiredReservationsReleased: expiredReservationsReleased,
                Duration: elapsed,
                LeaseId: lease.LeaseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reconciliation");
            throw;
        }
    }

    private static string GetProductLeaseName(string productId) => $"inventory-{productId}";

    private void OnLeaseRenewed(object? sender, LeaseRenewedEventArgs e)
    {
        _logger.LogDebug(
            "Lease {LeaseId} for {LeaseName} renewed. New expiration: {Expiration}",
            e.LeaseId, e.LeaseName, e.NewExpiration);
    }

    private void OnLeaseRenewalFailed(object? sender, LeaseRenewalFailedEventArgs e)
    {
        _logger.LogWarning(
            "Lease {LeaseId} renewal failed (attempt {Attempt}). Will retry: {WillRetry}. Error: {Error}",
            e.LeaseId, e.AttemptNumber, e.WillRetry, e.Exception.Message);
    }

    private void OnLeaseLost(object? sender, LeaseLostEventArgs e)
    {
        _logger.LogError(
            "Lease {LeaseId} for {LeaseName} was lost! Reason: {Reason}",
            e.LeaseId, e.LeaseName, e.Reason);
    }

    private static void InitializeSampleData()
    {
        if (_inventory.IsEmpty)
        {
            _inventory["PROD-001"] = new ProductInventory("PROD-001", 100);
            _inventory["PROD-002"] = new ProductInventory("PROD-002", 50);
            _inventory["PROD-003"] = new ProductInventory("PROD-003", 200);
        }
    }
}

/// <summary>
/// Internal representation of product inventory.
/// </summary>
internal class ProductInventory
{
    public string ProductId { get; }
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }

    public ProductInventory(string productId, int totalQuantity)
    {
        ProductId = productId;
        TotalQuantity = totalQuantity;
        AvailableQuantity = totalQuantity;
        ReservedQuantity = 0;
    }
}

/// <summary>
/// Extended reservation record with ProductId for internal tracking.
/// </summary>
internal class ReservationInternal
{
    public string ReservationId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public DateTimeOffset ReservedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string ProductId { get; init; } = string.Empty;

    public Reservation ToReservation() => new(ReservationId, CustomerId, Quantity, ReservedAt, ExpiresAt);
}
