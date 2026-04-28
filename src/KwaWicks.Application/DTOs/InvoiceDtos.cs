namespace KwaWicks.Application.DTOs;

// ── Hub-side: create invoice directly (existing flow) ──────────────────────
public class CreateInvoiceRequest
{
    public string CustomerId { get; set; } = "";
    public string HubId { get; set; } = "";

    public string DeliveryAddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";

    public string PaymentType { get; set; } = "";
    public string SaleType { get; set; } = "Delivery"; // Delivery | HubDirect
    public string StaffMemberId { get; set; } = "";

    public List<CreateInvoiceLine> Lines { get; set; } = new();

    /// <summary>Only required when PaymentType = "Split".</summary>
    public List<SplitPaymentLineRequest>? SplitPayments { get; set; }

    /// <summary>Optional phone to save on the client if they don't already have one.</summary>
    public string? ClientPhone { get; set; }
}

public class SplitPaymentLineRequest
{
    public string Method { get; set; } = ""; // Cash | Card | EFT
    public decimal Amount { get; set; }
}

public class CreateInvoiceLine
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
}

// ── Driver-side: create invoice from a delivery order ──────────────────────
public class CreateInvoiceFromDeliveryRequest
{
    public string CreatedByDriverId { get; set; } = "";
    public List<CreateInvoiceFromDeliveryLine> Lines { get; set; } = new();

    /// <summary>Optional phone to save on the client if they don't already have one.</summary>
    public string? ClientPhone { get; set; }
}

public class CreateInvoiceFromDeliveryLine
{
    public string SpeciesId { get; set; } = "";
    public int DeliveredQty { get; set; }
    public int ReturnedDeadQty { get; set; }
    public int ReturnedMutilatedQty { get; set; }
    public int ReturnedNotWantedQty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
}

// ── Payment ────────────────────────────────────────────────────────────────
public class RecordPaymentRequest
{
    public string PaymentType { get; set; } = ""; // Cash, EFT, Credit, CardMachine, Split

    /// <summary>Only required when PaymentType = "Split".</summary>
    public List<SplitPaymentLineRequest>? SplitPayments { get; set; }
}

// ── Receipt upload URL (EFT) ───────────────────────────────────────────────
public class ReceiptUploadUrlResponse
{
    public string PresignedUrl { get; set; } = "";
    public string S3Key { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

// ── Owner: update invoice line prices & resend WhatsApp ───────────────────
public class UpdateInvoiceLinesRequest
{
    public List<UpdateInvoiceLineRequest> Lines { get; set; } = new();
}

public class UpdateInvoiceLineRequest
{
    public string SpeciesId { get; set; } = "";

    /// <summary>New unit price entered inclusive of VAT. Back-calculated to ex-VAT internally.</summary>
    public decimal UnitPriceIncl { get; set; }
}

public class UpdateInvoiceLinesResponse
{
    public InvoiceResponse Invoice { get; set; } = null!;
    public bool WhatsAppSent { get; set; }
    public string? WhatsAppError { get; set; }
}

// ── Reconciliation ─────────────────────────────────────────────────────────
public class ReconRequest
{
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReceivedAt { get; set; }
}

public class ReconInvoiceItem
{
    public string InvoiceId { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string SaleType { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public decimal GrandTotal { get; set; }
    public string ReceiptS3Key { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string ReconReference { get; set; } = "";
    public string ReconNotes { get; set; } = "";
    public DateTime? ReconciledAt { get; set; }
    public int DaysOutstanding { get; set; }
}

// ── Response DTO ───────────────────────────────────────────────────────────
public class InvoiceResponse
{
    public string InvoiceId { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public string SaleType { get; set; } = "Delivery";
    public string StaffMemberId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string HubId { get; set; } = "";
    public string DeliveryOrderId { get; set; } = "";
    public string CreatedByDriverId { get; set; } = "";
    public string Status { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public string ReceiptS3Key { get; set; } = "";
    public decimal SubTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public List<InvoiceLineResponse> Lines { get; set; } = new();
    public List<SplitPaymentLineResponse> SplitPayments { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string ReconReference { get; set; } = "";
    public string ReconNotes { get; set; } = "";
    public DateTime? ReconciledAt { get; set; }
}

public class SplitPaymentLineResponse
{
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
}

public class InvoiceLineResponse
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }
}
