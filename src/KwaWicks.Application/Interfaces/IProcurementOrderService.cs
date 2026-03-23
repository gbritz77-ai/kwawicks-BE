using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IProcurementOrderService
{
    Task<ProcurementOrderResponse> CreateAsync(CreateProcurementOrderRequest request, string createdByUserId, CancellationToken ct = default);
    Task<ProcurementOrderResponse?> GetAsync(string id, CancellationToken ct = default);
    Task<List<ProcurementOrderResponse>> ListAsync(string? status = null, string? supplierId = null, CancellationToken ct = default);
    Task SubmitAsync(string id, CancellationToken ct = default);
    Task CompleteAsync(string id, CancellationToken ct = default);
    Task AdvanceStatusAsync(string id, string newStatus, CancellationToken ct = default);
    Task<ProcurementInvoiceUploadUrlResponse> GetInvoiceUploadUrlAsync(string id, CancellationToken ct = default);
}
