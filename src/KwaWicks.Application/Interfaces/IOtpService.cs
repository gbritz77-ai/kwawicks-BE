using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IOtpService
{
    /// <summary>Generate, save and WhatsApp an OTP for the given invoice.</summary>
    Task<OtpRecordResponse> SendAsync(
        string invoiceId, string referenceType,
        string clientId, string clientName, string clientPhone,
        string invoiceNumber, decimal invoiceTotal,
        CancellationToken ct);

    /// <summary>Verify the code. On success, triggers invoice WhatsApp and marks Confirmed.</summary>
    Task<OtpRecordResponse> VerifyAsync(
        string invoiceId, string code, string? userId, CancellationToken ct);

    /// <summary>Resend the OTP (generates a fresh code, resets expiry).</summary>
    Task<OtpRecordResponse> ResendAsync(string invoiceId, CancellationToken ct);

    /// <summary>Skip OTP and send the invoice anyway — records the bypass.</summary>
    Task<OtpRecordResponse> BypassAsync(
        string invoiceId, string? userId, string? reason, CancellationToken ct);

    /// <summary>Audit report, optionally scoped to one client and/or date range.</summary>
    Task<List<OtpRecordResponse>> GetReportAsync(
        string? clientId, DateTime? from, DateTime? to, CancellationToken ct);
}
