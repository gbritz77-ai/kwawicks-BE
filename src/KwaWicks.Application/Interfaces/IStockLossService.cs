using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IStockLossService
{
    /// <summary>Records dead/lost stock, decrements QtyOnHandHub for the species.</summary>
    Task<StockLossResponse> RecordLossAsync(RecordStockLossRequest request, string recordedByUserId, CancellationToken ct = default);

    Task<List<StockLossResponse>> ListAsync(string? speciesId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}
