namespace KwaWicks.Application.DTOs;

public class SendInvoiceWhatsAppRequest
{
    public string Phone { get; set; } = "";
}

public class SendStatementWhatsAppRequest
{
    public string Phone { get; set; } = "";
    public string? From { get; set; }
    public string? To { get; set; }
}

public class WhatsAppSendResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
