using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IClientService
{
    Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken ct = default);
    Task<ClientDto?> GetByIdAsync(string clientId, CancellationToken ct = default);
    Task<List<ClientDto>> ListAsync(int limit = 50, CancellationToken ct = default);
    Task<ClientDto?> UpdateAsync(string clientId, UpdateClientRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string clientId, CancellationToken ct = default);
}