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

    public List<CreateInvoiceLine> Lines { get; set; } = new();

    /// <summary>Optional phone to save on the client if they don't already have one.</summary>
    public string? ClientPhone { get; set; }
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
    public string PaymentType { get; set; } = ""; // Cash, EFT, Credit
}

// ── Receipt upload URL (EFT) ───────────────────────────────────────────────
public class ReceiptUploadUrlResponse
{
    public string PresignedUrl { get; set; } = "";
    public string S3Key { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

// ── Response DTO ───────────────────────────────────────────────────────────
public class InvoiceResponse
{
    public string InvoiceId { get; set; } = "";
    public string InvoiceNumber { get; set; } = ""; // e.g. INV000001
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InvoiceLineResponse
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }
}
