using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IBankStatementRepository
{
    Task<BankStatement> CreateAsync(BankStatement statement, CancellationToken ct);
    Task<BankStatement?> GetAsync(string statementId, CancellationToken ct);
    Task<BankStatement> UpdateAsync(BankStatement statement, CancellationToken ct);
    Task<List<BankStatement>> ListAsync(CancellationToken ct);
}
