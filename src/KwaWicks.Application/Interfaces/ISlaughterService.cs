using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface ISlaughterService
{
    Task<SlaughterBatchResponse> CreateAsync(CreateSlaughterRequest request, string? userId, CancellationToken ct);
    Task<List<SlaughterBatchResponse>> ListAsync(CancellationToken ct);
    Task<SlaughterBatchResponse?> GetAsync(string batchId, CancellationToken ct);
}
