using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface ICostAverageService
{
    /// <summary>
    /// Calculate weighted-average cost for every species that had stock movements in
    /// the given month. Optionally writes the result back to species.UnitCost.
    /// </summary>
    Task<List<CostAverageRecordResponse>> CalculateAsync(
        CalculateCostAverageRequest request, CancellationToken ct);

    /// <summary>Full cost-average history, optionally filtered to one species.</summary>
    Task<List<CostAverageRecordResponse>> GetHistoryAsync(
        string? speciesId, CancellationToken ct);
}
