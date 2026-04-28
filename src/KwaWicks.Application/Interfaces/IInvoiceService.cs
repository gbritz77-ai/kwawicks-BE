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

    /// <summary>Owner-only: update unit prices on an existing invoice. Recalculates all totals.</summary>
    Task<InvoiceResponse> UpdateLinesAsync(string invoiceId, UpdateInvoiceLinesRequest request, CancellationToken ct);

    /// <summary>Finance: list invoices for reconciliation. CustomerName is left empty — caller enriches it.</summary>
    Task<List<ReconInvoiceItem>> GetReconListAsync(string? paymentType, string? reconStatus, DateTime? from, DateTime? to, CancellationToken ct);

    /// <summary>Finance: mark an invoice as reconciled and confirm payment.</summary>
    Task ReconAsync(string invoiceId, ReconRequest request, CancellationToken ct);
}
