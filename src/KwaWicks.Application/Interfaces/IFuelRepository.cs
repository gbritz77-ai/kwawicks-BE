using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IFuelRepository
{
    Task<FuelIssue> CreateAsync(FuelIssue issue, CancellationToken ct);
    Task<FuelIssue?> GetAsync(string issueId, CancellationToken ct);
    Task<FuelIssue> UpdateAsync(FuelIssue issue, CancellationToken ct);
    Task<List<FuelIssue>> ListAsync(CancellationToken ct);
}
