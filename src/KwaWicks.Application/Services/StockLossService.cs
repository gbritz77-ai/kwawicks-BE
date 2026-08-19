using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class StockLossService : IStockLossService
{
    private readonly IStockLossRepository _repo;
    private readonly ISpeciesRepository _speciesRepo;

    public StockLossService(IStockLossRepository repo, ISpeciesRepository speciesRepo)
    {
        _repo = repo;
        _speciesRepo = speciesRepo;
    }

    public async Task<StockLossResponse> RecordLossAsync(
        RecordStockLossRequest request, string recordedByUserId, CancellationToken ct = default)
    {
        if (request.Qty <= 0)
            throw new ArgumentException("Quantity must be greater than zero.");

        var type = string.Equals(request.AdjustmentType, "Over", StringComparison.OrdinalIgnoreCase)
            ? "Over"
            : string.Equals(request.AdjustmentType, "Short", StringComparison.OrdinalIgnoreCase)
                ? "Short"
                : "Under";

        var species = await _speciesRepo.GetAsync(request.SpeciesId, ct)
            ?? throw new InvalidOperationException($"Species '{request.SpeciesId}' not found.");

        var isDeduction = type == "Under" || type == "Short";
        if (isDeduction && request.Qty > species.QtyOnHandHub)
            throw new ArgumentException(
                $"Cannot record a stock reduction of {request.Qty} — only {species.QtyOnHandHub} units are on hand.");

        var delta = type == "Over" ? +request.Qty : -request.Qty;
        await _speciesRepo.AdjustStockAsync(request.SpeciesId, delta, 0, ct,
            minOnHandRequired: isDeduction ? request.Qty : 0);

        var loss = new StockLoss
        {
            SpeciesId        = species.SpeciesId,
            SpeciesName      = species.Name,
            Qty              = request.Qty,
            AdjustmentType   = type,
            Notes            = request.Notes?.Trim() ?? "",
            RecordedByUserId = recordedByUserId,
        };

        await _repo.AddAsync(loss, ct);

        var qtyAfter = species.QtyOnHandHub + delta;
        return Map(loss, qtyAfter);
    }

    public async Task<List<StockLossResponse>> ListAsync(
        string? speciesId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var losses = await _repo.ListAsync(speciesId, from, to, ct);
        return losses.Select(l => Map(l, -1)).ToList();
    }

    private static StockLossResponse Map(StockLoss l, int qtyAfter) => new()
    {
        LossId            = l.LossId,
        SpeciesId         = l.SpeciesId,
        SpeciesName       = l.SpeciesName,
        Qty               = l.Qty,
        AdjustmentType    = l.AdjustmentType,
        Notes             = l.Notes,
        RecordedByUserId  = l.RecordedByUserId,
        CreatedAt         = l.CreatedAt,
        QtyOnHandHubAfter = qtyAfter,
    };
}
