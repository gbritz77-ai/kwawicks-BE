using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IStockLossRepository
{
    Task<StockLoss> AddAsync(StockLoss loss, CancellationToken ct = default);
    Task<List<StockLoss>> ListAsync(string? speciesId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}
