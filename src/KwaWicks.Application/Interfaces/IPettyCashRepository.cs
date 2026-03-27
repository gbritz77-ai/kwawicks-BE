using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IPettyCashRepository
{
    Task<PettyCashEntry> CreateEntryAsync(PettyCashEntry entry, CancellationToken ct);
    Task<List<PettyCashEntry>> ListEntriesAsync(string? from, string? to, CancellationToken ct);
    Task<List<PettyCashEntry>> ListOpenEntriesAsync(CancellationToken ct);
    Task MarkEntriesCashedUpAsync(IEnumerable<string> entryIds, string cashupId, CancellationToken ct);

    Task<PettyCashup> CreateCashupAsync(PettyCashup cashup, CancellationToken ct);
    Task<PettyCashup?> GetLatestCashupAsync(CancellationToken ct);
    Task<List<PettyCashup>> ListCashupsAsync(CancellationToken ct);
}
