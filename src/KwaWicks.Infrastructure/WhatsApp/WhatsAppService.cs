using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Infrastructure.WhatsApp;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _http;

    public WhatsAppService(HttpClient http)
    {
        _http = http;
    }

    private static string FormatPhone(string phone)
    {
        phone = phone.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
        if (phone.StartsWith("0"))
            phone = "27" + phone[1..];
        else if (!phone.StartsWith("27"))
            phone = "27" + phone;
        return phone;
    }

    private static string FormatTotal(decimal total) =>
        $"R {total:N2}";

    public async Task SendInvoiceAsync(
        string toPhone,
        string recipientName,
        string invoiceNumber,
        decimal grandTotal,
        string pdfUrl,
        string pdfFilename,
        CancellationToken ct = default)
    {
        var phoneNumberId = Environment.GetEnvironmentVariable("WHATSAPP_PHONE_NUMBER_ID")
            ?? throw new InvalidOperationException("WHATSAPP_PHONE_NUMBER_ID is not set.");
        var accessToken = Environment.GetEnvironmentVariable("WHATSAPP_ACCESS_TOKEN")
            ?? throw new InvalidOperationException("WHATSAPP_ACCESS_TOKEN is not set.");

        var to = FormatPhone(toPhone);

        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = "kwawicks_invoice",
                language = new { code = "en_US" },
                components = new object[]
                {
                    new
                    {
                        type = "header",
                        parameters = new[]
                        {
                            new
                            {
                                type = "document",
                                document = new { link = pdfUrl, filename = pdfFilename }
                            }
                        }
                    },
                    new
                    {
                        type = "body",
                        parameters = new[]
                        {
                            new { type = "text", text = recipientName },
                            new { type = "text", text = invoiceNumber },
                            new { type = "text", text = FormatTotal(grandTotal) }
                        }
                    }
                }
            }
        };

        await SendRequestAsync(phoneNumberId, accessToken, payload, ct);
    }

    public async Task SendStatementAsync(
        string toPhone,
        string recipientName,
        string pdfUrl,
        string pdfFilename,
        CancellationToken ct = default)
    {
        var phoneNumberId = Environment.GetEnvironmentVariable("WHATSAPP_PHONE_NUMBER_ID")
            ?? throw new InvalidOperationException("WHATSAPP_PHONE_NUMBER_ID is not set.");
        var accessToken = Environment.GetEnvironmentVariable("WHATSAPP_ACCESS_TOKEN")
            ?? throw new InvalidOperationException("WHATSAPP_ACCESS_TOKEN is not set.");

        var to = FormatPhone(toPhone);

        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = "kwawicks_statement",
                language = new { code = "en_US" },
                components = new object[]
                {
                    new
                    {
                        type = "header",
                        parameters = new[]
                        {
                            new
                            {
                                type = "document",
                                document = new { link = pdfUrl, filename = pdfFilename }
                            }
                        }
                    },
                    new
                    {
                        type = "body",
                        parameters = new[]
                        {
                            new { type = "text", text = recipientName }
                        }
                    }
                }
            }
        };

        await SendRequestAsync(phoneNumberId, accessToken, payload, ct);
    }

    private async Task SendRequestAsync(string phoneNumberId, string accessToken, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.facebook.com/v21.0/{phoneNumberId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"WhatsApp API error {(int)response.StatusCode}: {body}");
    }
}
