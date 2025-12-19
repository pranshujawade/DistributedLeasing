using DistributedLeasing.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CriticalSectionFunction;

/// <summary>
/// Azure Function that demonstrates using distributed leasing for critical section protection.
/// Multiple function instances compete for a lease to ensure only one instance processes
/// inventory updates at a time.
/// </summary>
public class InventoryFunctions
{
    private readonly ILogger<InventoryFunctions> _logger;
    private readonly IInventoryService _inventoryService;

    public InventoryFunctions(
        ILogger<InventoryFunctions> logger,
        IInventoryService inventoryService)
    {
        _logger = logger;
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// HTTP-triggered function to reserve inventory for a product.
    /// Uses distributed lease to ensure atomic inventory operations.
    /// </summary>
    [Function("ReserveInventory")]
    public async Task<HttpResponseData> ReserveInventory(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "inventory/{productId}/reserve")]
        HttpRequestData req,
        string productId)
    {
        _logger.LogInformation("Processing inventory reservation for product: {ProductId}", productId);

        try
        {
            // Parse request body
            var body = await JsonSerializer.DeserializeAsync<ReserveInventoryRequest>(req.Body);
            
            if (body == null || body.Quantity <= 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid quantity" });
                return badResponse;
            }

            // Reserve inventory using distributed lease for critical section
            var result = await _inventoryService.ReserveInventoryAsync(
                productId,
                body.Quantity,
                body.CustomerId);

            var response = req.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.Conflict);
            await response.WriteAsJsonAsync(result);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving inventory for product {ProductId}", productId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
            return errorResponse;
        }
    }

    /// <summary>
    /// HTTP-triggered function to release reserved inventory.
    /// Uses distributed lease to ensure atomic inventory operations.
    /// </summary>
    [Function("ReleaseInventory")]
    public async Task<HttpResponseData> ReleaseInventory(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "inventory/{productId}/release")]
        HttpRequestData req,
        string productId)
    {
        _logger.LogInformation("Processing inventory release for product: {ProductId}", productId);

        try
        {
            var body = await JsonSerializer.DeserializeAsync<ReleaseInventoryRequest>(req.Body);
            
            if (body == null || body.Quantity <= 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid quantity" });
                return badResponse;
            }

            var result = await _inventoryService.ReleaseInventoryAsync(
                productId,
                body.Quantity,
                body.CustomerId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing inventory for product {ProductId}", productId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
            return errorResponse;
        }
    }

    /// <summary>
    /// HTTP-triggered function to get current inventory status.
    /// </summary>
    [Function("GetInventory")]
    public async Task<HttpResponseData> GetInventory(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "inventory/{productId}")]
        HttpRequestData req,
        string productId)
    {
        _logger.LogInformation("Getting inventory for product: {ProductId}", productId);

        try
        {
            var inventory = await _inventoryService.GetInventoryAsync(productId);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(inventory);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory for product {ProductId}", productId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
            return errorResponse;
        }
    }

    /// <summary>
    /// Timer-triggered function that processes pending inventory reconciliation.
    /// Only one instance will acquire the lease and perform the reconciliation.
    /// </summary>
    [Function("ReconcileInventory")]
    public async Task ReconcileInventory(
        [TimerTrigger("0 */5 * * * *")] // Every 5 minutes
        TimerInfo timerInfo)
    {
        _logger.LogInformation("Inventory reconciliation triggered at: {Time}", DateTime.UtcNow);

        try
        {
            var result = await _inventoryService.ReconcileInventoryAsync();
            
            if (result.WasProcessed)
            {
                _logger.LogInformation(
                    "? Reconciliation completed by this instance. Products reconciled: {Count}",
                    result.ProductsReconciled);
            }
            else
            {
                _logger.LogInformation(
                    "Another instance is performing reconciliation");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inventory reconciliation");
        }
    }
}

/// <summary>
/// Request model for reserving inventory.
/// </summary>
public record ReserveInventoryRequest(int Quantity, string CustomerId);

/// <summary>
/// Request model for releasing inventory.
/// </summary>
public record ReleaseInventoryRequest(int Quantity, string CustomerId);
