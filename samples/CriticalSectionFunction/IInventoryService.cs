namespace CriticalSectionFunction;

/// <summary>
/// Service interface for inventory management operations.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Reserves inventory for a product using distributed leasing to ensure atomicity.
    /// </summary>
    Task<ReservationResult> ReserveInventoryAsync(string productId, int quantity, string customerId);

    /// <summary>
    /// Releases previously reserved inventory.
    /// </summary>
    Task<ReleaseResult> ReleaseInventoryAsync(string productId, int quantity, string customerId);

    /// <summary>
    /// Gets current inventory status for a product.
    /// </summary>
    Task<InventoryStatus> GetInventoryAsync(string productId);

    /// <summary>
    /// Performs inventory reconciliation across all products.
    /// Uses distributed lease to ensure only one instance runs reconciliation.
    /// </summary>
    Task<ReconciliationResult> ReconcileInventoryAsync();
}

/// <summary>
/// Result of an inventory reservation attempt.
/// </summary>
public record ReservationResult(
    bool Success,
    string ProductId,
    int RequestedQuantity,
    int AvailableQuantity,
    string? ReservationId,
    string Message,
    bool AcquiredLease);

/// <summary>
/// Result of an inventory release operation.
/// </summary>
public record ReleaseResult(
    bool Success,
    string ProductId,
    int ReleasedQuantity,
    int NewAvailableQuantity,
    string Message);

/// <summary>
/// Current inventory status for a product.
/// </summary>
public record InventoryStatus(
    string ProductId,
    int TotalQuantity,
    int AvailableQuantity,
    int ReservedQuantity,
    List<Reservation> ActiveReservations);

/// <summary>
/// Represents an active inventory reservation.
/// </summary>
public record Reservation(
    string ReservationId,
    string CustomerId,
    int Quantity,
    DateTimeOffset ReservedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Result of inventory reconciliation operation.
/// </summary>
public record ReconciliationResult(
    bool WasProcessed,
    int ProductsReconciled,
    int ExpiredReservationsReleased,
    TimeSpan Duration,
    string? LeaseId);
