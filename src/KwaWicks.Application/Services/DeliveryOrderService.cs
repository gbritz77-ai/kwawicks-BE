using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class DeliveryOrderService : IDeliveryOrderService
{
    private readonly IDeliveryOrderRepository _deliveryRepo;
    private readonly ISpeciesRepository _speciesRepo;
    private readonly IHubTaskRepository _hubTaskRepo;
    private readonly IInvoiceRepository _invoiceRepo;

    public DeliveryOrderService(
        IDeliveryOrderRepository deliveryRepo,
        ISpeciesRepository speciesRepo,
        IHubTaskRepository hubTaskRepo,
        IInvoiceRepository invoiceRepo)
    {
        _deliveryRepo = deliveryRepo ?? throw new ArgumentNullException(nameof(deliveryRepo));
        _speciesRepo = speciesRepo ?? throw new ArgumentNullException(nameof(speciesRepo));
        _hubTaskRepo = hubTaskRepo ?? throw new ArgumentNullException(nameof(hubTaskRepo));
        _invoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
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

                // Fetch only to get the species name for error messages and the unit price
                var species = await _speciesRepo.GetAsync(line.SpeciesId, ct);
                if (species == null)
                    throw new InvalidOperationException($"Species not found: {line.SpeciesId}");

                // Atomic ADD: deduct on-hand, add to booked. Condition ensures stock is sufficient.
                await _speciesRepo.AdjustStockAsync(line.SpeciesId, -line.Quantity, +line.Quantity, ct, minOnHandRequired: line.Quantity);
                bookedOut.Add((line.SpeciesId, line.Quantity));

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
                    // Atomic rollback: reverse the deduction
                    await _speciesRepo.AdjustStockAsync(speciesId, +qty, -qty, CancellationToken.None);
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

        // Only include orders where the driver has NOT yet submitted a return.
        // Once ReturnSubmitted = true the stock has been queued for hub check-in and
        // ReturnedNotWantedQty was already added back to QtyOnHandHub during invoice
        // creation — exposing it here again would double-count it.
        var linesWithReturns = orders
            .Where(o => !o.ReturnSubmitted && !o.ReturnCheckedIn)
            .SelectMany(o => o.Lines)
            .Where(l => l.TotalReturnedQty > 0)
            .ToList();

        if (linesWithReturns.Count == 0)
            return new List<DriverStockItem>();

        // Fetch species names for lookup
        var allSpecies = await _speciesRepo.ListAsync(ct);
        var speciesById = allSpecies.ToDictionary(s => s.SpeciesId, s => s);

        return linesWithReturns
            .GroupBy(l => l.SpeciesId)
            .Select(g =>
            {
                speciesById.TryGetValue(g.Key, out var sp);
                return new DriverStockItem
                {
                    SpeciesId = g.Key,
                    SpeciesName = sp?.Name ?? g.Key,
                    AvailableQty = g.Sum(l => l.TotalReturnedQty),
                    UnitPrice = g.Max(l => l.UnitPrice),
                    Vat = sp?.Vat ?? 0.15m
                };
            })
            .Where(i => i.AvailableQty > 0)
            .OrderBy(i => i.SpeciesName)
            .ToList();
    }

    public async Task HubDropAsync(string deliveryOrderId, HubDropRequest request, CancellationToken ct)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        if (order.Status != "OutForDelivery")
            throw new InvalidOperationException("Hub drops can only be recorded for orders that are OutForDelivery.");

        var adjusted = new List<(string speciesId, int qty)>();
        try
        {
            foreach (var drop in request.Lines)
            {
                if (drop.Qty <= 0) continue;

                var line = order.Lines.FirstOrDefault(l => l.SpeciesId == drop.SpeciesId)
                    ?? throw new InvalidOperationException($"Species {drop.SpeciesId} is not on this delivery order.");

                int maxDroppable = line.Quantity - line.HubDropQty;
                if (drop.Qty > maxDroppable)
                    throw new InvalidOperationException(
                        $"Cannot drop {drop.Qty} for species {drop.SpeciesId} — only {maxDroppable} available to drop (ordered: {line.Quantity}, already dropped: {line.HubDropQty}).");

                // Move dropped qty from booked → on-hand immediately
                await _speciesRepo.AdjustStockAsync(drop.SpeciesId, onHandDelta: +drop.Qty, bookedDelta: -drop.Qty, ct);
                adjusted.Add((drop.SpeciesId, drop.Qty));

                line.HubDropQty += drop.Qty;
            }

            order.UpdatedAt = DateTime.UtcNow;
            await _deliveryRepo.UpdateAsync(order, ct);
        }
        catch
        {
            foreach (var (speciesId, qty) in adjusted)
            {
                try { await _speciesRepo.AdjustStockAsync(speciesId, onHandDelta: -qty, bookedDelta: +qty, CancellationToken.None); }
                catch { /* swallow rollback errors */ }
            }
            throw;
        }
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
            line.ReturnedToHubQty = submitted?.Qty ?? line.TotalReturnedQty; // default: return all returned qty
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

        // Add returned stock back to hub using the driver-reported ReturnedToHubQty.
        // Reconcile against ReturnedNotWantedQty (set at invoice time) so only the delta
        // between what the driver is actually handing back and what was already credited
        // to inventory is adjusted — preventing double-counting.
        var adjusted = new List<(string speciesId, int qty)>();
        try
        {
            foreach (var line in order.Lines.Where(l => l.ReturnedToHubQty > 0))
            {
                ct.ThrowIfCancellationRequested();
                // ReturnedNotWantedQty was already added to QtyOnHandHub during invoice creation.
                // Only adjust for the difference (e.g. driver returns more than originally recorded).
                int delta = line.ReturnedToHubQty - line.TotalReturnedQty;
                if (delta != 0)
                {
                    await _speciesRepo.AdjustStockAsync(line.SpeciesId, delta, 0, ct);
                    adjusted.Add((line.SpeciesId, delta));
                }
            }

            order.ReturnCheckedIn = true;
            order.UpdatedAt = DateTime.UtcNow;
            await _deliveryRepo.UpdateAsync(order, ct);
        }
        catch
        {
            foreach (var (speciesId, qty) in adjusted)
            {
                try { await _speciesRepo.AdjustStockAsync(speciesId, -qty, 0, CancellationToken.None); }
                catch { /* swallow rollback errors */ }
            }
            throw;
        }
    }

    public async Task EditLinesAsync(string deliveryOrderId, EditDeliveryOrderLinesRequest request, CancellationToken ct)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one line is required.");

        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        if (order.Status != "Open")
            throw new InvalidOperationException("Lines can only be edited on Open delivery orders.");

        foreach (var edit in request.Lines)
        {
            if (edit.Quantity <= 0)
                throw new ArgumentException($"Quantity must be greater than 0 for species {edit.SpeciesId}.");
        }

        // Apply each edited line — adjust stock for qty changes
        var adjustedSpecies = new List<(string speciesId, int delta)>();
        try
        {
            foreach (var edit in request.Lines)
            {
                ct.ThrowIfCancellationRequested();

                var line = order.Lines.FirstOrDefault(l => l.SpeciesId == edit.SpeciesId);
                if (line == null)
                    throw new InvalidOperationException($"Species {edit.SpeciesId} not found on this order.");

                int delta = edit.Quantity - line.Quantity; // positive = need more, negative = releasing

                if (delta != 0)
                {
                    // Atomic adjustment; condition enforces sufficient on-hand for increases
                    int minRequired = delta > 0 ? delta : 0;
                    await _speciesRepo.AdjustStockAsync(edit.SpeciesId, -delta, +delta, ct, minOnHandRequired: minRequired);
                    adjustedSpecies.Add((edit.SpeciesId, delta));
                }

                line.Quantity = edit.Quantity;
                line.UnitPrice = edit.UnitPrice;
            }

            order.UpdatedAt = DateTime.UtcNow;
            await _deliveryRepo.UpdateAsync(order, ct);
        }
        catch
        {
            // Roll back any species adjustments already made
            foreach (var (speciesId, delta) in adjustedSpecies)
            {
                try
                {
                    await _speciesRepo.AdjustStockAsync(speciesId, +delta, -delta, CancellationToken.None);
                }
                catch { /* swallow rollback errors */ }
            }
            throw;
        }
    }

    public async Task RecordReturnsInspectionAsync(string deliveryOrderId, RecordReturnsInspectionRequest request, CancellationToken ct)
    {
        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        if (order.Status != "Delivered" && order.Status != "MarkedAtHub")
            throw new InvalidOperationException("Returns inspection can only be recorded for Delivered orders.");

        var adjusted = new List<(string speciesId, int qty)>();
        try
        {
            foreach (var insp in request.Lines)
            {
                var line = order.Lines.FirstOrDefault(l => l.SpeciesId == insp.SpeciesId)
                    ?? throw new InvalidOperationException($"Species {insp.SpeciesId} not on this order.");

                int totalDamaged = insp.DeadQty + insp.MutilatedQty;
                int previousDamaged = line.InspectedDeadQty + line.InspectedMutilatedQty;
                int delta = totalDamaged - previousDamaged; // net stock deduction vs prior inspection

                if (delta != 0)
                {
                    await _speciesRepo.AdjustStockAsync(line.SpeciesId, -delta, 0, ct);
                    adjusted.Add((line.SpeciesId, delta));
                }

                line.InspectedDeadQty = insp.DeadQty;
                line.InspectedMutilatedQty = insp.MutilatedQty;
                line.ReturnsInspected = true;
            }

            // Cash confirmation: compute expected cash from invoice and store discrepancy
            if (!string.IsNullOrEmpty(order.InvoiceId))
            {
                var invoice = await _invoiceRepo.GetAsync(order.InvoiceId, ct);
                if (invoice != null)
                {
                    decimal expectedCash = 0m;
                    if (invoice.PaymentType == "Cash")
                        expectedCash = invoice.GrandTotal;
                    else if (invoice.PaymentType == "Split")
                        expectedCash = invoice.SplitPayments.Where(sp => sp.Method == "Cash").Sum(sp => sp.Amount);

                    if (expectedCash > 0)
                    {
                        order.CashExpected = expectedCash;

                        if (request.CashReceived.HasValue)
                        {
                            order.CashReceived = request.CashReceived;
                            order.CashConfirmed = true;
                            order.CashDiscrepancy = request.CashReceived.Value - expectedCash;
                        }
                    }
                }
            }

            order.UpdatedAt = DateTime.UtcNow;
            await _deliveryRepo.UpdateAsync(order, ct);
        }
        catch
        {
            foreach (var (speciesId, qty) in adjusted)
            {
                try { await _speciesRepo.AdjustStockAsync(speciesId, +qty, 0, CancellationToken.None); }
                catch { /* swallow rollback errors */ }
            }
            throw;
        }
    }

    public async Task DeleteAsync(string deliveryOrderId, CancellationToken ct)
    {
        var order = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        if (order.Status != "Open")
            throw new InvalidOperationException(
                $"Only Open orders can be deleted. This order is '{order.Status}'.");

        // Atomically restore stock for every line before deleting
        foreach (var line in order.Lines)
        {
            await _speciesRepo.AdjustStockAsync(line.SpeciesId, +line.Quantity, -line.Quantity, ct);
        }

        await _deliveryRepo.DeleteAsync(deliveryOrderId, ct);
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
        CashExpected = order.CashExpected,
        CashReceived = order.CashReceived,
        CashConfirmed = order.CashConfirmed,
        CashDiscrepancy = order.CashDiscrepancy,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        Lines = order.Lines.Select(l => new DeliveryOrderLineResponse
        {
            SpeciesId = l.SpeciesId,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            DeliveredQty = l.DeliveredQty,
            TotalReturnedQty = l.TotalReturnedQty,
            ReturnedToHubQty = l.ReturnedToHubQty,
            ReturnsInspected = l.ReturnsInspected,
            InspectedDeadQty = l.InspectedDeadQty,
            InspectedMutilatedQty = l.InspectedMutilatedQty,
            HubDropQty = l.HubDropQty
        }).ToList()
    };
}
