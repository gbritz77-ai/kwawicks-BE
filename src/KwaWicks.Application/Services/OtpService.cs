using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class OtpService : IOtpService
{
    private const int OtpValidMinutes = 30;

    private readonly IOtpRepository _repo;
    private readonly IWhatsAppService _whatsApp;
    private readonly IInvoiceNotificationService _notification;

    public OtpService(
        IOtpRepository repo,
        IWhatsAppService whatsApp,
        IInvoiceNotificationService notification)
    {
        _repo = repo;
        _whatsApp = whatsApp;
        _notification = notification;
    }

    public async Task<OtpRecordResponse> SendAsync(
        string invoiceId, string referenceType,
        string clientId, string clientName, string clientPhone,
        string invoiceNumber, decimal invoiceTotal,
        CancellationToken ct)
    {
        var code = Random.Shared.Next(100_000, 999_999).ToString();
        var now  = DateTime.UtcNow;

        var record = new OtpRecord
        {
            InvoiceId     = invoiceId,
            InvoiceNumber = invoiceNumber,
            ReferenceType = referenceType,
            ClientId      = clientId,
            ClientName    = clientName,
            ClientPhone   = clientPhone,
            InvoiceTotal  = invoiceTotal,
            OtpCode       = code,
            Status        = "Pending",
            SentAt        = now,
            ExpiresAt     = now.AddMinutes(OtpValidMinutes),
        };

        await _repo.SaveAsync(record, ct);

        var totalFmt = invoiceTotal.ToString("N2");
        var message  =
            $"Your confirmation OTP is: *{code}*\n\n" +
            $"Invoice: {invoiceNumber}  |  Total: R {totalFmt}\n" +
            $"This code is valid for {OtpValidMinutes} minutes.\n" +
            $"Share it with the driver or staff to confirm your transaction.";

        await _whatsApp.SendHubRequestAsync(clientPhone, "Sale Confirmation", message, ct);

        return ToResponse(record);
    }

    public async Task<OtpRecordResponse> VerifyAsync(
        string invoiceId, string code, string? userId, CancellationToken ct)
    {
        var record = await _repo.GetByInvoiceIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException("No OTP found for this invoice.");

        if (record.Status == "Confirmed")
            throw new InvalidOperationException("OTP already confirmed.");

        if (record.Status == "Bypassed")
            throw new InvalidOperationException("OTP was bypassed — invoice already sent.");

        if (DateTime.UtcNow > record.ExpiresAt)
        {
            record.Status = "Expired";
            await _repo.SaveAsync(record, ct);
            throw new InvalidOperationException("OTP has expired. Please resend.");
        }

        if (record.OtpCode != code.Trim())
            throw new InvalidOperationException("Incorrect OTP code.");

        record.Status           = "Confirmed";
        record.ConfirmedAt      = DateTime.UtcNow;
        record.ConfirmedByUserId= userId;
        await _repo.SaveAsync(record, ct);

        // Send the invoice WhatsApp now that the client has confirmed
        if (!string.IsNullOrWhiteSpace(record.ClientPhone))
            await _notification.TrySendInvoiceWhatsAppAsync(record.InvoiceId, record.ClientPhone, ct);

        return ToResponse(record);
    }

    public async Task<OtpRecordResponse> ResendAsync(string invoiceId, CancellationToken ct)
    {
        var existing = await _repo.GetByInvoiceIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException("No OTP found for this invoice.");

        if (existing.Status is "Confirmed" or "Bypassed")
            throw new InvalidOperationException($"OTP is already {existing.Status}.");

        // Generate a fresh code and overwrite
        return await SendAsync(
            existing.InvoiceId, existing.ReferenceType,
            existing.ClientId, existing.ClientName, existing.ClientPhone,
            existing.InvoiceNumber, existing.InvoiceTotal, ct);
    }

    public async Task<OtpRecordResponse> BypassAsync(
        string invoiceId, string? userId, string? reason, CancellationToken ct)
    {
        var record = await _repo.GetByInvoiceIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException("No OTP found for this invoice.");

        if (record.Status is "Confirmed" or "Bypassed")
            throw new InvalidOperationException($"OTP is already {record.Status}.");

        record.Status            = "Bypassed";
        record.ConfirmedAt       = DateTime.UtcNow;
        record.ConfirmedByUserId = userId;
        record.BypassReason      = reason ?? "Bypassed by admin";
        await _repo.SaveAsync(record, ct);

        // Still send the invoice
        if (!string.IsNullOrWhiteSpace(record.ClientPhone))
            await _notification.TrySendInvoiceWhatsAppAsync(record.InvoiceId, record.ClientPhone, ct);

        return ToResponse(record);
    }

    public async Task<List<OtpRecordResponse>> GetReportAsync(
        string? clientId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var records = string.IsNullOrWhiteSpace(clientId)
            ? await _repo.ListAllAsync(from, to, ct)
            : await _repo.ListByClientAsync(clientId, ct);

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            if (from.HasValue) records = records.Where(r => r.SentAt >= from.Value).ToList();
            if (to.HasValue)   records = records.Where(r => r.SentAt <= to.Value).ToList();
        }

        return records.Select(ToResponse).ToList();
    }

    private static OtpRecordResponse ToResponse(OtpRecord r) => new()
    {
        OtpId             = r.OtpId,
        InvoiceId         = r.InvoiceId,
        InvoiceNumber     = r.InvoiceNumber,
        ReferenceType     = r.ReferenceType,
        ClientId          = r.ClientId,
        ClientName        = r.ClientName,
        ClientPhone       = r.ClientPhone,
        InvoiceTotal      = r.InvoiceTotal,
        Status            = r.Status,
        SentAt            = r.SentAt,
        ExpiresAt         = r.ExpiresAt,
        ConfirmedAt       = r.ConfirmedAt,
        ConfirmedByUserId = r.ConfirmedByUserId,
        BypassReason      = r.BypassReason,
    };
}
