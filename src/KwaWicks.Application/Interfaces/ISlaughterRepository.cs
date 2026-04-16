using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface ISlaughterRepository
{
    Task<SlaughterBatch> CreateAsync(SlaughterBatch batch, CancellationToken ct);
    Task<List<SlaughterBatch>> ListAsync(CancellationToken ct);
    Task<SlaughterBatch?> GetAsync(string batchId, CancellationToken ct);
}
