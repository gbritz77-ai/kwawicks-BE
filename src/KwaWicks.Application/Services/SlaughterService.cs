using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class SlaughterService : ISlaughterService
{
    private readonly ISlaughterRepository _repo;
    private readonly ISpeciesRepository _species;

    public SlaughterService(ISlaughterRepository repo, ISpeciesRepository species)
    {
        _repo = repo;
        _species = species;
    }

    public async Task<SlaughterBatchResponse> CreateAsync(
        CreateSlaughterRequest request, string? userId, CancellationToken ct)
    {
        if (request.SourceQty <= 0)
            throw new ArgumentException("Source quantity must be greater than zero.");
        if (!request.Yields.Any())
            throw new ArgumentException("At least one yield line is required.");

        // 1. Load and validate source species
        var source = await _species.GetAsync(request.SourceSpeciesId, ct)
            ?? throw new ArgumentException($"Species '{request.SourceSpeciesId}' not found.");

        if (source.QtyOnHandHub < request.SourceQty)
            throw new InvalidOperationException(
                $"Insufficient stock: {source.Name} has {source.QtyOnHandHub} on hand, " +
                $"but {request.SourceQty} requested for slaughter.");

        // 2. Determine effective source unit cost
        var sourceUnitCost = request.SourceUnitCost ?? source.UnitCost;

        // 3. Load all yield species up-front and validate
        var yieldSpecies = new Dictionary<string, Species>();
        foreach (var yl in request.Yields)
        {
            if (yl.Qty <= 0) throw new ArgumentException($"Yield quantity must be > 0 for species '{yl.SpeciesId}'.");
            var s = await _species.GetAsync(yl.SpeciesId, ct)
                ?? throw new ArgumentException($"Yield species '{yl.SpeciesId}' not found.");
            yieldSpecies[yl.SpeciesId] = s;
        }

        // 4. Deduct source stock
        source.QtyOnHandHub -= request.SourceQty;
        await _species.UpdateAsync(source, ct);

        // 5. Add yield stock and update prices
        var yieldLines = new List<SlaughterYieldLine>();
        foreach (var yl in request.Yields)
        {
            var sp = yieldSpecies[yl.SpeciesId];
            sp.QtyOnHandHub += yl.Qty;
            sp.UnitCost = yl.UnitCost;
            sp.SellPrice = yl.UnitPrice;
            await _species.UpdateAsync(sp, ct);

            yieldLines.Add(new SlaughterYieldLine
            {
                SpeciesId   = sp.SpeciesId,
                SpeciesName = sp.Name,
                Qty         = yl.Qty,
                UnitCost    = yl.UnitCost,
                UnitPrice   = yl.UnitPrice,
            });
        }

        // 6. Persist the batch record
        var batch = new SlaughterBatch
        {
            BatchId           = Guid.NewGuid().ToString("N"),
            SlaughteredAtUtc  = DateTime.UtcNow,
            SourceSpeciesId   = source.SpeciesId,
            SourceSpeciesName = source.Name,
            SourceQty         = request.SourceQty,
            SourceUnitCost    = sourceUnitCost,
            Yields            = yieldLines,
            Notes             = request.Notes,
            CreatedByUserId   = userId,
        };

        await _repo.CreateAsync(batch, ct);
        return ToResponse(batch);
    }

    public async Task<List<SlaughterBatchResponse>> ListAsync(CancellationToken ct)
    {
        var batches = await _repo.ListAsync(ct);
        return batches.Select(ToResponse).ToList();
    }

    public async Task<SlaughterBatchResponse?> GetAsync(string batchId, CancellationToken ct)
    {
        var batch = await _repo.GetAsync(batchId, ct);
        return batch is null ? null : ToResponse(batch);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static SlaughterBatchResponse ToResponse(SlaughterBatch b) => new()
    {
        BatchId           = b.BatchId,
        SlaughteredAtUtc  = b.SlaughteredAtUtc,
        SourceSpeciesId   = b.SourceSpeciesId,
        SourceSpeciesName = b.SourceSpeciesName,
        SourceQty         = b.SourceQty,
        SourceUnitCost    = b.SourceUnitCost,
        TotalInputCost    = b.TotalInputCost,
        Notes             = b.Notes,
        Yields            = b.Yields.Select(y => new SlaughterYieldLineResponse
        {
            SpeciesId   = y.SpeciesId,
            SpeciesName = y.SpeciesName,
            Qty         = y.Qty,
            UnitCost    = y.UnitCost,
            UnitPrice   = y.UnitPrice,
        }).ToList(),
    };
}
