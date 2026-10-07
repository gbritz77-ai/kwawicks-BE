using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class InboundShipmentService : IInboundShipmentService
{
    private readonly IInboundShipmentRepository _repo;
    private readonly ISupplierRepository _supplierRepo;
    private readonly ISpeciesRepository _speciesRepo;

    public InboundShipmentService(
        IInboundShipmentRepository repo,
        ISupplierRepository supplierRepo,
        ISpeciesRepository speciesRepo)
    {
        _repo = repo;
        _supplierRepo = supplierRepo;
        _speciesRepo = speciesRepo;
    }

    public async Task<InboundShipmentResponse> CreateAsync(
        CreateInboundShipmentRequest request,
        string createdByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierId))
            throw new ArgumentException("SupplierId is required.");
        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one line is required.");

        var supplier = await _supplierRepo.GetAsync(request.SupplierId, ct)
            ?? throw new InvalidOperationException($"Supplier not found: {request.SupplierId}");

        var allSpecies = await _speciesRepo.ListAsync(ct);
        var speciesMap = allSpecies.ToDictionary(s => s.SpeciesId, s => s);

        var shipment = new InboundShipment
        {
            SupplierId = request.SupplierId,
            SupplierName = supplier.Name,
            TransporterName = request.TransporterName ?? "",
            TransporterContact = request.TransporterContact ?? "",
            VehicleReg = request.VehicleReg ?? "",
            Notes = request.Notes ?? "",
            CreatedByUserId = createdByUserId,
            ExpectedAt = request.ExpectedAt,
            Status = "Open",
            Lines = request.Lines.Select(l =>
            {
                speciesMap.TryGetValue(l.SpeciesId, out var sp);
                return new InboundShipmentLine
                {
                    SpeciesId = l.SpeciesId,
                    SpeciesName = sp?.Name ?? l.SpeciesId,
                    OrderedQty = l.OrderedQty,
                    UnitCost = l.UnitCost,
                };
            }).ToList()
        };

        await _repo.CreateAsync(shipment, ct);
        return MapToResponse(shipment);
    }

    public async Task<InboundShipmentResponse?> GetAsync(string id, CancellationToken ct = default)
    {
        var s = await _repo.GetAsync(id, ct);
        return s == null ? null : MapToResponse(s);
    }

    public async Task<List<InboundShipmentResponse>> ListAsync(string? status = null, string? supplierId = null, CancellationToken ct = default)
    {
        var list = await _repo.ListAsync(status, supplierId, ct);
        return list.Select(MapToResponse).ToList();
    }

    public async Task<InboundShipmentResponse> UpdateAsync(string id, CreateInboundShipmentRequest request, CancellationToken ct = default)
    {
        var shipment = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Inbound shipment not found: {id}");

        if (shipment.Status != "Open")
            throw new InvalidOperationException("Only Open shipments can be edited.");

        var supplier = await _supplierRepo.GetAsync(request.SupplierId, ct)
            ?? throw new InvalidOperationException($"Supplier not found: {request.SupplierId}");

        var allSpecies = await _speciesRepo.ListAsync(ct);
        var speciesMap = allSpecies.ToDictionary(s => s.SpeciesId, s => s);

        shipment.SupplierId = request.SupplierId;
        shipment.SupplierName = supplier.Name;
        shipment.TransporterName = request.TransporterName ?? "";
        shipment.TransporterContact = request.TransporterContact ?? "";
        shipment.VehicleReg = request.VehicleReg ?? "";
        shipment.Notes = request.Notes ?? "";
        shipment.ExpectedAt = request.ExpectedAt;
        shipment.Lines = request.Lines.Select(l =>
        {
            speciesMap.TryGetValue(l.SpeciesId, out var sp);
            return new InboundShipmentLine
            {
                SpeciesId = l.SpeciesId,
                SpeciesName = sp?.Name ?? l.SpeciesId,
                OrderedQty = l.OrderedQty,
                UnitCost = l.UnitCost,
            };
        }).ToList();

        await _repo.UpdateAsync(shipment, ct);
        return MapToResponse(shipment);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var shipment = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Inbound shipment not found: {id}");

        if (shipment.Status != "Open")
            throw new InvalidOperationException("Only Open shipments can be deleted.");

        await _repo.DeleteAsync(id, ct);
    }

    public async Task MarkDispatchedAsync(string id, CancellationToken ct = default)
    {
        var shipment = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Inbound shipment not found: {id}");

        if (shipment.Status != "Open")
            throw new InvalidOperationException("Only Open shipments can be marked as Dispatched.");

        shipment.Status = "Dispatched";
        shipment.DispatchedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(shipment, ct);
    }

    public async Task ReceiveAsync(string id, ReceiveInboundShipmentRequest request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var shipment = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Inbound shipment not found: {id}");

        if (shipment.Status != "Dispatched" && shipment.Status != "Open")
            throw new InvalidOperationException("Shipment must be Open or Dispatched to receive.");

        var adjusted = new List<(string speciesId, int qty)>();
        try
        {
            foreach (var recv in request.Lines)
            {
                if (recv.ReceivedQty < 0)
                    throw new ArgumentException($"ReceivedQty cannot be negative for species {recv.SpeciesId}.");

                var line = shipment.Lines.FirstOrDefault(l => l.SpeciesId == recv.SpeciesId)
                    ?? throw new InvalidOperationException($"Species {recv.SpeciesId} is not on this shipment.");

                line.ReceivedQty = recv.ReceivedQty;
                line.ShortfallNotes = recv.ShortfallNotes ?? "";

                if (recv.ReceivedQty > 0)
                {
                    // Add received stock to hub on-hand immediately
                    await _speciesRepo.AdjustStockAsync(recv.SpeciesId, +recv.ReceivedQty, 0, ct);
                    adjusted.Add((recv.SpeciesId, recv.ReceivedQty));
                }
            }

            shipment.Status = "Received";
            shipment.ReceivedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(shipment, ct);
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

    private static InboundShipmentResponse MapToResponse(InboundShipment s) => new()
    {
        InboundShipmentId = s.InboundShipmentId,
        SupplierId = s.SupplierId,
        SupplierName = s.SupplierName,
        HubId = s.HubId,
        Status = s.Status,
        TransporterName = s.TransporterName,
        TransporterContact = s.TransporterContact,
        VehicleReg = s.VehicleReg,
        Notes = s.Notes,
        CreatedByUserId = s.CreatedByUserId,
        ExpectedAt = s.ExpectedAt,
        DispatchedAt = s.DispatchedAt,
        ReceivedAt = s.ReceivedAt,
        CompletedAt = s.CompletedAt,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        Lines = s.Lines.Select(l => new InboundShipmentLineResponse
        {
            SpeciesId = l.SpeciesId,
            SpeciesName = l.SpeciesName,
            OrderedQty = l.OrderedQty,
            UnitCost = l.UnitCost,
            ReceivedQty = l.ReceivedQty,
            ShortfallNotes = l.ShortfallNotes,
        }).ToList()
    };
}
