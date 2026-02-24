using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface ISpeciesRepository
{
    Task<Species> CreateAsync(Species species, CancellationToken ct);
    Task<List<Species>> ListAsync(CancellationToken ct);
    Task<Species?> GetAsync(string speciesId, CancellationToken ct);
    Task<Species?> UpdateAsync(Species species, CancellationToken ct);
}
