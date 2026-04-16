namespace KwaWicks.Domain.Entities;

/// <summary>
/// OTP confirmation record for a sale or delivery.
/// PK = OTP#{InvoiceId}, SK = META
/// </summary>
public class OtpRecord
{
    public string OtpId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The invoice this OTP is gating.</summary>
    public string InvoiceId { get; set; } = default!;
    public string InvoiceNumber { get; set; } = "";

    /// <summary>"HubSale" | "DriverSale"</summary>
    public string ReferenceType { get; set; } = default!;

    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ClientPhone { get; set; } = "";
    public decimal InvoiceTotal { get; set; }

    /// <summary>6-digit numeric code.</summary>
    public string OtpCode { get; set; } = default!;

    /// <summary>Pending | Confirmed | Expired | Bypassed</summary>
    public string Status { get; set; } = "Pending";

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByUserId { get; set; }
    public string? BypassReason { get; set; }
}
