using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IBankTransactionRepository
{
    Task<BankTransaction?> GetAsync(string transactionId, CancellationToken ct);
    Task<List<BankTransaction>> ListAsync(CancellationToken ct);
    Task CreateAsync(BankTransaction tx, CancellationToken ct);
    Task UpdateAsync(BankTransaction tx, CancellationToken ct);
    Task BatchCreateAsync(IEnumerable<BankTransaction> transactions, CancellationToken ct);
    Task<HashSet<string>> GetDedupKeysAsync(CancellationToken ct);
    Task<(int ImportCount, DateTime LastImportedAt)> GetImportConfigAsync(CancellationToken ct);
    Task SaveImportConfigAsync(int importCount, DateTime lastImportedAt, CancellationToken ct);
}
