using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IInvoiceService
{
    Task<string> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct);
    Task<string> CreateFromDeliveryAsync(string deliveryOrderId, CreateInvoiceFromDeliveryRequest request, CancellationToken ct);
    Task<InvoiceResponse?> GetAsync(string invoiceId, CancellationToken ct);
    Task<List<InvoiceResponse>> ListAsync(string? hubId, string? customerId, CancellationToken ct);
    Task RecordPaymentAsync(string invoiceId, RecordPaymentRequest request, CancellationToken ct);
    Task ConfirmPaymentAsync(string invoiceId, CancellationToken ct);
    Task<ReceiptUploadUrlResponse> GetReceiptUploadUrlAsync(string invoiceId, CancellationToken ct);
    Task<string> GetReceiptViewUrlAsync(string invoiceId, CancellationToken ct);
}
