using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class CostAverageService : ICostAverageService
{
    private readonly ICostAverageRepository _repo;
    private readonly ISpeciesRepository _speciesRepo;
    private readonly IProcurementOrderRepository _poRepo;
    private readonly ISlaughterRepository _slaughterRepo;

    public CostAverageService(
        ICostAverageRepository repo,
        ISpeciesRepository speciesRepo,
        IProcurementOrderRepository poRepo,
        ISlaughterRepository slaughterRepo)
    {
        _repo = repo;
        _speciesRepo = speciesRepo;
        _poRepo = poRepo;
        _slaughterRepo = slaughterRepo;
    }

    public async Task<List<CostAverageRecordResponse>> CalculateAsync(
        CalculateCostAverageRequest request, CancellationToken ct)
    {
        if (request.Year < 2000 || request.Year > 2100)
            throw new ArgumentException("Invalid year.");
        if (request.Month < 1 || request.Month > 12)
            throw new ArgumentException("Month must be between 1 and 12.");

        var monthLabel = $"{request.Year:D4}-{request.Month:D2}";
        var monthStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd   = monthStart.AddMonths(1);

        // ── Gather contributions per species ──────────────────────────────────

        // Key: speciesId → list of contribution lines
        var contributions = new Dictionary<string, List<CostContributionLine>>();

        // 1. Completed procurement orders received in the month
        var allOrders = await _poRepo.ListAsync(ct: ct);
        foreach (var order in allOrders)
        {
            // Use UpdatedAt as the "received" date for completed/delivered orders
            if (order.Status != "Completed" && order.Status != "DeliveredToHub") continue;
            if (order.UpdatedAt < monthStart || order.UpdatedAt >= monthEnd) continue;

            foreach (var line in order.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.SpeciesId) || line.OrderedQty <= 0) continue;
                Add(contributions, line.SpeciesId, new CostContributionLine
                {
                    Source         = "ProcurementOrder",
                    SourceId       = order.ProcurementOrderId,
                    SourceRef      = order.SupplierOrderReference,
                    Date           = order.UpdatedAt,
                    Qty            = line.OrderedQty,
                    UnitCostExVat  = line.UnitCost,
                });
            }
        }

        // 2. Slaughter batch yield costs received in the month
        var allBatches = await _slaughterRepo.ListAsync(ct);
        foreach (var batch in allBatches)
        {
            if (batch.SlaughteredAtUtc < monthStart || batch.SlaughteredAtUtc >= monthEnd) continue;

            foreach (var yield in batch.Yields)
            {
                if (string.IsNullOrWhiteSpace(yield.SpeciesId) || yield.Qty <= 0) continue;
                Add(contributions, yield.SpeciesId, new CostContributionLine
                {
                    Source         = "SlaughterBatch",
                    SourceId       = batch.BatchId,
                    SourceRef      = batch.Notes ?? "",
                    Date           = batch.SlaughteredAtUtc,
                    Qty            = yield.Qty,
                    UnitCostExVat  = yield.UnitCost,
                });
            }
        }

        if (!contributions.Any())
            return new List<CostAverageRecordResponse>();

        // ── Calculate weighted averages and persist ───────────────────────────

        var results = new List<CostAverageRecordResponse>();

        foreach (var (speciesId, lines) in contributions)
        {
            var species = await _speciesRepo.GetAsync(speciesId, ct);
            if (species is null) continue;

            var totalQty    = lines.Sum(l => l.Qty);
            var weightedSum = lines.Sum(l => l.Qty * l.UnitCostExVat);
            var avgExVat    = totalQty > 0 ? weightedSum / totalQty : 0m;
            var avgIncVat   = avgExVat * (1 + species.Vat);
            var priorCost   = species.UnitCost;

            var record = new CostAverageRecord
            {
                SpeciesId        = speciesId,
                SpeciesName      = species.Name,
                Month            = monthLabel,
                TotalQty         = totalQty,
                AvgCostExVat     = Math.Round(avgExVat, 4),
                AvgCostIncVat    = Math.Round(avgIncVat, 4),
                VatRate          = species.Vat,
                PriorUnitCost    = priorCost,
                AppliedToSpecies = request.ApplyToSpecies,
                CalculatedAt     = DateTime.UtcNow,
                Sources          = lines,
            };

            await _repo.UpsertAsync(record, ct);

            // Optionally push the average back to the species
            if (request.ApplyToSpecies)
            {
                species.UnitCost = record.AvgCostExVat;
                await _speciesRepo.UpdateAsync(species, ct);
            }

            results.Add(ToResponse(record));
        }

        return results.OrderBy(r => r.SpeciesName).ToList();
    }

    public async Task<List<CostAverageRecordResponse>> GetHistoryAsync(
        string? speciesId, CancellationToken ct)
    {
        var records = string.IsNullOrWhiteSpace(speciesId)
            ? await _repo.ListAllAsync(ct)
            : await _repo.ListBySpeciesAsync(speciesId, ct);

        return records.Select(ToResponse).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Add(
        Dictionary<string, List<CostContributionLine>> dict,
        string key,
        CostContributionLine line)
    {
        if (!dict.TryGetValue(key, out var list))
            dict[key] = list = new List<CostContributionLine>();
        list.Add(line);
    }

    private static CostAverageRecordResponse ToResponse(CostAverageRecord r) => new()
    {
        SpeciesId        = r.SpeciesId,
        SpeciesName      = r.SpeciesName,
        Month            = r.Month,
        TotalQty         = r.TotalQty,
        AvgCostExVat     = r.AvgCostExVat,
        AvgCostIncVat    = r.AvgCostIncVat,
        VatRate          = r.VatRate,
        PriorUnitCost    = r.PriorUnitCost,
        CostDelta        = r.AvgCostExVat - r.PriorUnitCost,
        AppliedToSpecies = r.AppliedToSpecies,
        CalculatedAt     = r.CalculatedAt,
        Sources          = r.Sources.Select(s => new CostContributionLineResponse
        {
            Source        = s.Source,
            SourceId      = s.SourceId,
            SourceRef     = s.SourceRef,
            Date          = s.Date,
            Qty           = s.Qty,
            UnitCostExVat = s.UnitCostExVat,
        }).ToList(),
    };
}
