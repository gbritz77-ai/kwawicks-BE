using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface ICollectionRequestService
{
    Task<CollectionRequestResponse> CreateAsync(CreateCollectionRequestRequest request, CancellationToken ct = default);
    Task<CollectionRequestResponse?> GetAsync(string id, CancellationToken ct = default);
    Task<List<CollectionRequestResponse>> ListAsync(string? driverId = null, string? status = null, string? procurementOrderId = null, CancellationToken ct = default);
    Task<CollectionRequestResponse> DriverLoadAsync(string id, DriverLoadingUpdateRequest request, CancellationToken ct = default);
    Task<CollectionRequestResponse> DispatchAsync(string id, CancellationToken ct = default);
    Task<CollectionRequestResponse> ArriveAsync(string id, CancellationToken ct = default);
    Task<CollectionRequestResponse> HubConfirmAsync(string id, HubConfirmReceiptRequest request, CancellationToken ct = default);
    Task<CollectionRequestResponse> FinanceAcknowledgeAsync(string id, string invoiceS3Key, CancellationToken ct = default);
    Task<CollectionInvoiceUploadUrlResponse> GetInvoiceUploadUrlAsync(string id, CancellationToken ct = default);
    Task<CollectionInvoiceUploadUrlResponse> GetDeliveryNoteUploadUrlAsync(string id, CancellationToken ct = default);
    Task<string> GetDeliveryNoteViewUrlAsync(string id, CancellationToken ct = default);
    Task<CollectionRequestResponse> AddDeliveryAllocationAsync(string id, AddDeliveryAllocationRequest request, CancellationToken ct = default);
}
