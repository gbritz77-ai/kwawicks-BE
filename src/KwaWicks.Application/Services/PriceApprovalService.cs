using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class PriceApprovalService : IPriceApprovalService
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly ISpeciesRepository _speciesRepo;
    private readonly IWhatsAppService _whatsApp;

    public PriceApprovalService(
        IInvoiceRepository invoiceRepo,
        ISpeciesRepository speciesRepo,
        IWhatsAppService whatsApp)
    {
        _invoiceRepo = invoiceRepo;
        _speciesRepo = speciesRepo;
        _whatsApp = whatsApp;
    }

    public async Task<bool> CheckAndFlagAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct);
        if (invoice is null) return false;

        var belowCostLines = new List<BelowCostLine>();

        foreach (var line in invoice.Lines)
        {
            var species = await _speciesRepo.GetAsync(line.SpeciesId, ct);
            if (species is null) continue;

            // line.UnitPrice is ex-VAT; compare directly against species.UnitCost
            if (line.UnitPrice < species.UnitCost)
            {
                belowCostLines.Add(new BelowCostLine
                {
                    SpeciesId   = line.SpeciesId,
                    SpeciesName = species.Name,
                    Quantity    = line.Quantity,
                    CostPrice   = species.UnitCost,
                    SalePrice   = line.UnitPrice
                });
            }
        }

        if (belowCostLines.Count == 0) return false;

        invoice.PriceApprovalStatus = "Pending";
        invoice.BelowCostLines = belowCostLines;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoiceRepo.UpdateAsync(invoice, ct);

        // Send WhatsApp alert to all configured admin phones (fire-and-forget errors)
        var adminPhones = (Environment.GetEnvironmentVariable("ADMIN_NOTIFY_PHONES") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (adminPhones.Length > 0)
        {
            var linesSummary = string.Join("\n", belowCostLines.Select(l =>
                $"- {l.SpeciesName} x{l.Quantity}: sold R{l.SalePrice:F2} (cost R{l.CostPrice:F2}, shortfall R{(l.CostPrice - l.SalePrice) * l.Quantity:F2})"));

            var message = $"Invoice {invoice.InvoiceNumber} was created with items sold below cost:\n{linesSummary}\n\nPlease review and approve in the KwaWicks app.";

            foreach (var phone in adminPhones)
            {
                try
                {
                    await _whatsApp.SendHubRequestAsync(phone, "Below-Cost Alert", message, ct);
                }
                catch
                {
                    // Non-fatal — alert failure must never block the sale
                }
            }
        }

        return true;
    }

    public async Task<List<PriceApprovalResponse>> GetPendingAsync(CancellationToken ct)
    {
        var invoices = await _invoiceRepo.ListByPriceApprovalStatusAsync("Pending", ct);
        return invoices
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new PriceApprovalResponse
            {
                InvoiceId           = i.InvoiceId,
                InvoiceNumber       = i.InvoiceNumber,
                CustomerId          = i.CustomerId,
                SaleType            = i.SaleType,
                PaymentType         = i.PaymentType,
                GrandTotal          = i.GrandTotal,
                PriceApprovalStatus = i.PriceApprovalStatus,
                CreatedAt           = i.CreatedAt,
                BelowCostLines      = i.BelowCostLines.Select(b => new BelowCostLineDto
                {
                    SpeciesId   = b.SpeciesId,
                    SpeciesName = b.SpeciesName,
                    Quantity    = b.Quantity,
                    CostPrice   = b.CostPrice,
                    SalePrice   = b.SalePrice
                }).ToList()
            })
            .ToList();
    }

    public async Task ApproveAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        if (invoice.PriceApprovalStatus != "Pending")
            throw new InvalidOperationException("Invoice is not pending price approval.");

        invoice.PriceApprovalStatus = "Approved";
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoiceRepo.UpdateAsync(invoice, ct);
    }

    public async Task AmendAndApproveAsync(string invoiceId, AmendPriceRequest request, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        if (invoice.PriceApprovalStatus != "Pending")
            throw new InvalidOperationException("Invoice is not pending price approval.");

        // Apply amended prices to invoice lines
        foreach (var amend in request.Lines)
        {
            var line = invoice.Lines.FirstOrDefault(l => l.SpeciesId == amend.SpeciesId);
            if (line is null) continue;
            line.UnitPrice = amend.NewUnitPrice;
            line.LineTotal = amend.NewUnitPrice * line.Quantity * (1 + line.VatRate);
        }

        // Recalculate totals
        invoice.SubTotal   = invoice.Lines.Sum(l => l.UnitPrice * l.Quantity);
        invoice.VatTotal   = invoice.Lines.Sum(l => l.UnitPrice * l.Quantity * l.VatRate);
        invoice.GrandTotal = invoice.SubTotal + invoice.VatTotal;

        invoice.PriceApprovalStatus = "Amended";
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoiceRepo.UpdateAsync(invoice, ct);
    }
}
