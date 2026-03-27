using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IPettyCashService
{
    Task<PettyCashEntryDto> CreateEntryAsync(CreatePettyCashEntryRequest request, string recordedBy, CancellationToken ct);
    Task<List<PettyCashEntryDto>> ListEntriesAsync(string? from, string? to, CancellationToken ct);
    Task<PettyCashSummaryDto> GetSummaryAsync(CancellationToken ct);
    Task<PettyCashupDto> CreateCashupAsync(CreateCashupRequest request, string closedBy, CancellationToken ct);
    Task<List<PettyCashupDto>> ListCashupsAsync(CancellationToken ct);
    Task<List<PettyCashEntryDto>> ListDriverEntriesAsync(string driverId, CancellationToken ct);
    Task<string> GetSlipUploadUrlAsync(string entryId, CancellationToken ct);
    Task<PettyCashEntryDto> ConfirmSlipUploadedAsync(string entryId, string s3Key, CancellationToken ct);
}
