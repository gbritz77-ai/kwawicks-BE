namespace KwaWicks.Application.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public class VerifyOtpRequest
{
    public string Code { get; set; } = "";
}

public class BypassOtpRequest
{
    public string? Reason { get; set; }
}

// ── Responses ─────────────────────────────────────────────────────────────────

public class OtpRecordResponse
{
    public string OtpId { get; set; } = default!;
    public string InvoiceId { get; set; } = default!;
    public string InvoiceNumber { get; set; } = "";
    public string ReferenceType { get; set; } = default!;
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public decimal InvoiceTotal { get; set; }
    public string Status { get; set; } = default!;
    public DateTime SentAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByUserId { get; set; }
    public string? BypassReason { get; set; }

    /// <summary>True when Status == "Confirmed" or "Bypassed".</summary>
    public bool IsResolved => Status is "Confirmed" or "Bypassed";
}
