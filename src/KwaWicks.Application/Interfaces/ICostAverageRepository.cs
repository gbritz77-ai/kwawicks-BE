using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface ICostAverageRepository
{
    /// <summary>Insert or overwrite the record for the given species + month.</summary>
    Task UpsertAsync(CostAverageRecord record, CancellationToken ct);

    /// <summary>All monthly records for one species, sorted oldest→newest.</summary>
    Task<List<CostAverageRecord>> ListBySpeciesAsync(string speciesId, CancellationToken ct);

    /// <summary>All records across all species for a given month (YYYY-MM).</summary>
    Task<List<CostAverageRecord>> ListByMonthAsync(string month, CancellationToken ct);

    /// <summary>All records, all species, all months — for the full report.</summary>
    Task<List<CostAverageRecord>> ListAllAsync(CancellationToken ct);
}
