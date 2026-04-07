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

    public async Task<List<DriverStockItem>> GetDriverAvailableStockAsync(string driverId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(driverId))
            return new List<DriverStockItem>();

        var orders = await _deliveryRepo.ListAsync(driverId, null, "Delivered", ct);

        // Only include orders where the driver has NOT yet submitted a return
        // Once ReturnSubmitted = true, those items are queued for hub check-in
        var linesWithReturns = orders
            .Where(o => !o.ReturnSubmitted)
            .SelectMany(o => o.Lines)
            .Where(l => l.ReturnedNotWantedQty > 0)
            .ToList();

        if (linesWithReturns.Count == 0)
            return new List<DriverStockItem>();

        // Fetch species names for lookup
        var allSpecies = await _speciesRepo.ListAsync(ct);
        var speciesById = allSpecies.ToDictionary(s => s.SpeciesId, s => s.Name);

        return linesWithReturns
            .GroupBy(l => l.SpeciesId)
            .Select(g => new DriverStockItem
            {
                SpeciesId = g.Key,
                SpeciesName = speciesById.TryGetValue(g.Key, out var name) ? name : g.Key,
                AvailableQty = g.Sum(l => l.ReturnedNotWantedQty),
                UnitPrice = g.Max(l => l.UnitPrice)
            })
            .Where(i => i.AvailableQty > 0)
            .OrderBy(i => i.SpeciesName)
            .ToList();
    }

    public async Task SubmitReturnAsync(string deliveryOrderId, SubmitReturnRequest request, CancellationToken ct)
    {
        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        if (order.Status != "Delivered")
            throw new InvalidOperationException("Can only submit returns for delivered orders.");

        if (order.ReturnSubmitted)
            throw new InvalidOperationException("Return has already been submitted for this order.");

        // Record how many of each species the driver is returning
        foreach (var line in order.Lines)
        {
            var submitted = request.Lines.FirstOrDefault(l => l.SpeciesId == line.SpeciesId);
            line.ReturnedToHubQty = submitted?.Qty ?? line.ReturnedNotWantedQty; // default: return all not-wanted
        }

        order.ReturnSubmitted = true;
        order.UpdatedAt = DateTime.UtcNow;
        await _deliveryRepo.UpdateAsync(order, ct);

        // Create a hub task so staff know to expect the return
        var returnSummary = string.Join(", ", order.Lines
            .Where(l => l.ReturnedToHubQty > 0)
            .Select(l => $"{l.ReturnedToHubQty}x {l.SpeciesId}"));

        var hubTask = new HubTask
        {
            HubId = order.HubId,
            Type = "StockReturn",
            Status = "Open",
            DeliveryOrderId = order.DeliveryOrderId,
            Title = $"Stock Return — {order.AssignedDriverName} returning: {returnSummary}"
        };
        await _hubTaskRepo.CreateAsync(hubTask, ct);
    }

    public async Task CheckInReturnAsync(string deliveryOrderId, CancellationToken ct)
    {
        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        if (!order.ReturnSubmitted)
            throw new InvalidOperationException("Driver has not submitted a return for this order yet.");

        if (order.ReturnCheckedIn)
            throw new InvalidOperationException("Return has already been checked in.");

        order.ReturnCheckedIn = true;
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
        ReturnSubmitted = order.ReturnSubmitted,
        ReturnCheckedIn = order.ReturnCheckedIn,
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
            ReturnedNotWantedQty = l.ReturnedNotWantedQty,
            ReturnedToHubQty = l.ReturnedToHubQty
        }).ToList()
    };
}
