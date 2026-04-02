using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class DeliveryOrderService : IDeliveryOrderService
{
    private readonly IDeliveryOrderRepository _deliveryRepo;
    private readonly ISpeciesRepository _speciesRepo;
    private readonly IHubTaskRepository _hubTaskRepo;

    public DeliveryOrderService(
        IDeliveryOrderRepository deliveryRepo,
        ISpeciesRepository speciesRepo,
        IHubTaskRepository hubTaskRepo)
    {
        _deliveryRepo = deliveryRepo ?? throw new ArgumentNullException(nameof(deliveryRepo));
        _speciesRepo = speciesRepo ?? throw new ArgumentNullException(nameof(speciesRepo));
        _hubTaskRepo = hubTaskRepo ?? throw new ArgumentNullException(nameof(hubTaskRepo));
    }

    public async Task<string> CreateAsync(CreateDeliveryOrderRequest request, CancellationToken ct)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.CustomerId)) throw new ArgumentException("CustomerId is required.");
        if (string.IsNullOrWhiteSpace(request.HubId)) throw new ArgumentException("HubId is required.");
        if (string.IsNullOrWhiteSpace(request.AssignedDriverId)) throw new ArgumentException("AssignedDriverId is required.");
        if (request.Lines == null || request.Lines.Count == 0) throw new ArgumentException("At least one line is required.");

        foreach (var l in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(l.SpeciesId)) throw new ArgumentException("SpeciesId is required on all lines.");
            if (l.Quantity <= 0) throw new ArgumentException("Quantity must be greater than 0.");
        }

        var deliveryOrder = new DeliveryOrder
        {
            CustomerId = request.CustomerId,
            HubId = request.HubId,
            AssignedDriverId = request.AssignedDriverId,
            AssignedDriverName = request.AssignedDriverName,
            DeliveryAddressLine1 = request.DeliveryAddressLine1,
            City = request.City,
            Province = request.Province,
            PostalCode = request.PostalCode,
            Status = "Open",
            Lines = new List<DeliveryOrderLine>()
        };

        var bookedOut = new List<(string speciesId, int qty)>();

        try
        {
            foreach (var line in request.Lines)
            {
                ct.ThrowIfCancellationRequested();

                var species = await _speciesRepo.GetAsync(line.SpeciesId, ct);
                if (species == null)
                    throw new InvalidOperationException($"Species not found: {line.SpeciesId}");

                if (species.QtyOnHandHub < line.Quantity)
                    throw new InvalidOperationException(
                        $"Insufficient stock for {species.Name}. On hand: {species.QtyOnHandHub}, requested: {line.Quantity}");

                species.QtyOnHandHub -= line.Quantity;
                species.QtyBookedOutForDelivery += line.Quantity;
                await _speciesRepo.UpdateAsync(species, ct);
                bookedOut.Add((species.SpeciesId, line.Quantity));

                deliveryOrder.Lines.Add(new DeliveryOrderLine
                {
                    SpeciesId = line.SpeciesId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice ?? species.SellPrice ?? 0m
                });
            }

            await _deliveryRepo.CreateAsync(deliveryOrder, ct);

            var hubTask = new HubTask
            {
                HubId = request.HubId,
                Type = "Delivery",
                Status = "Open",
                DeliveryOrderId = deliveryOrder.DeliveryOrderId,
                Title = $"Delivery {deliveryOrder.DeliveryOrderId} - Assigned to {request.AssignedDriverName}"
            };
            await _hubTaskRepo.CreateAsync(hubTask, ct);

            return deliveryOrder.DeliveryOrderId;
        }
        catch
        {
            foreach (var (speciesId, qty) in bookedOut)
            {
                try
                {
                    var s = await _speciesRepo.GetAsync(speciesId, ct);
                    if (s != null)
                    {
                        s.QtyOnHandHub += qty;
                        s.QtyBookedOutForDelivery = Math.Max(0, s.QtyBookedOutForDelivery - qty);
                        await _speciesRepo.UpdateAsync(s, ct);
                    }
                }
                catch { /* swallow rollback errors */ }
            }
            throw;
        }
    }

    public async Task<DeliveryOrderResponse?> GetAsync(string deliveryOrderId, CancellationToken ct)
    {
        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct);
        return order == null ? null : MapToResponse(order);
    }

    public async Task<List<DeliveryOrderResponse>> ListAsync(string? driverId, string? hubId, string? status, CancellationToken ct)
    {
        var orders = await _deliveryRepo.ListAsync(driverId, hubId, status, ct);
        return orders.Select(MapToResponse).ToList();
    }

    public async Task UpdateStatusAsync(string deliveryOrderId, string status, CancellationToken ct)
    {
        var validStatuses = new[] { "Open", "OutForDelivery", "Delivered", "MarkedAtHub" };
        if (!validStatuses.Contains(status))
            throw new ArgumentException($"Invalid status '{status}'. Valid values: {string.Join(", ", validStatuses)}");

        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _deliveryRepo.UpdateAsync(order, ct);
    }

    private static DeliveryOrderResponse MapToResponse(DeliveryOrder order) => new()
    {
        DeliveryOrderId = order.DeliveryOrderId,
        InvoiceId = order.InvoiceId,
        HubId = order.HubId,
        CustomerId = order.CustomerId,
        AssignedDriverId = order.AssignedDriverId,
        AssignedDriverName = order.AssignedDriverName,
        Status = order.Status,
        DeliveryAddressLine1 = order.DeliveryAddressLine1,
        City = order.City,
        Province = order.Province,
        PostalCode = order.PostalCode,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        Lines = order.Lines.Select(l => new DeliveryOrderLineResponse
        {
            SpeciesId = l.SpeciesId,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            DeliveredQty = l.DeliveredQty,
            ReturnedDeadQty = l.ReturnedDeadQty,
            ReturnedMutilatedQty = l.ReturnedMutilatedQty,
            ReturnedNotWantedQty = l.ReturnedNotWantedQty
        }).ToList()
    };
}
