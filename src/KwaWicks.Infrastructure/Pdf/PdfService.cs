using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KwaWicks.Infrastructure.Pdf;

public class PdfService : IPdfService
{
    static PdfService()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    public Task<byte[]> GenerateInvoicePdfAsync(
        InvoiceResponse invoice,
        ClientDto client,
        IEnumerable<(string speciesId, string speciesName)> speciesNames,
        CancellationToken ct = default)
    {
        var speciesMap = speciesNames.ToDictionary(x => x.speciesId, x => x.speciesName);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // Header
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("KwaWicks")
                            .Bold().FontSize(24).FontColor("#166534");
                        row.RelativeItem().AlignRight().Text("TAX INVOICE")
                            .Bold().FontSize(16).FontColor("#64748b");
                    });

                    // Green divider
                    col.Item().PaddingVertical(8).LineHorizontal(2).LineColor("#166534");

                    // Bill To + Invoice details
                    col.Item().PaddingBottom(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Bill To:").Bold().FontSize(10).FontColor("#64748b");
                            c.Item().Text(client.ClientName).Bold().FontSize(12);
                            if (!string.IsNullOrWhiteSpace(client.ClientAddress))
                                c.Item().Text(client.ClientAddress).FontColor("#475569");
                            if (!string.IsNullOrWhiteSpace(client.ClientCity))
                                c.Item().Text(client.ClientCity + ", " + client.ClientProvince + " " + client.ClientPostalCode).FontColor("#475569");
                            if (!string.IsNullOrWhiteSpace(client.ClientPhone))
                                c.Item().Text(client.ClientPhone).FontColor("#475569");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Invoice #: " + invoice.InvoiceId.Substring(0, Math.Min(8, invoice.InvoiceId.Length))).Bold();
                            c.Item().Text("Date: " + invoice.CreatedAt.ToString("dd/MM/yyyy")).FontColor("#475569");
                            c.Item().Text("Status: " + invoice.Status).FontColor("#475569");
                            c.Item().Text("Payment: " + invoice.PaymentType).FontColor("#475569");
                        });
                    });

                    // Line items table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            static IContainer HeaderCell(IContainer c) =>
                                c.Background("#166534").Padding(6);

                            header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Qty").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Unit Price").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("VAT%").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Line Total").Bold().FontColor("#ffffff");
                        });

                        var alt = false;
                        foreach (var line in invoice.Lines)
                        {
                            var bg = alt ? "#f9fafb" : "#ffffff";
                            alt = !alt;

                            var name = speciesMap.TryGetValue(line.SpeciesId, out var sn) ? sn : line.SpeciesId;

                            IContainer Cell(IContainer c) => c.Background(bg).Padding(6).BorderBottom(1).BorderColor("#f1f5f9");

                            table.Cell().Element(Cell).Text(name);
                            table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString());
                            table.Cell().Element(Cell).AlignRight().Text("R " + line.UnitPrice.ToString("N2"));
                            table.Cell().Element(Cell).AlignRight().Text((line.VatRate * 100).ToString("0") + "%");
                            table.Cell().Element(Cell).AlignRight().Text("R " + line.LineTotal.ToString("N2"));
                        }
                    });

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem();
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Sub-Total").FontColor("#475569");
                                r.RelativeItem().AlignRight().Text("R " + invoice.SubTotal.ToString("N2"));
                            });
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("VAT").FontColor("#475569");
                                r.RelativeItem().AlignRight().Text("R " + invoice.VatTotal.ToString("N2"));
                            });
                            c.Item().PaddingTop(4).LineHorizontal(1).LineColor("#e2e8f0");
                            c.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("GRAND TOTAL").Bold();
                                r.RelativeItem().AlignRight().Text("R " + invoice.GrandTotal.ToString("N2")).Bold().FontSize(12);
                            });
                        });
                    });
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span("Thank you for your business!  |  KwaWicks  |  kwawicks.co.za")
                        .FontSize(9).FontColor("#94a3b8");
                });
            });
        });

        var bytes = doc.GeneratePdf();
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateStatementPdfAsync(
        CustomerStatementResponse statement,
        CancellationToken ct = default)
    {
        var periodLabel = statement.From.HasValue || statement.To.HasValue
            ? (statement.From.HasValue ? statement.From.Value.ToString("dd/MM/yyyy") : "—")
              + " to "
              + (statement.To.HasValue ? statement.To.Value.ToString("dd/MM/yyyy") : "—")
            : "All time";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // Header
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("KwaWicks").Bold().FontSize(24).FontColor("#166534");
                            c.Item().Text("ACCOUNT STATEMENT").Bold().FontSize(14).FontColor("#64748b");
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Generated: " + statement.GeneratedAt.ToString("dd/MM/yyyy")).FontColor("#475569");
                            c.Item().Text("Period: " + periodLabel).FontColor("#475569");
                        });
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(2).LineColor("#166534");

                    // Client info
                    col.Item().PaddingBottom(16).Column(c =>
                    {
                        c.Item().Text("Account:").Bold().FontSize(10).FontColor("#64748b");
                        c.Item().Text(statement.CustomerName).Bold().FontSize(13);
                        if (!string.IsNullOrWhiteSpace(statement.CustomerAddress))
                            c.Item().Text(statement.CustomerAddress).FontColor("#475569");
                        if (!string.IsNullOrWhiteSpace(statement.CustomerContact))
                            c.Item().Text(statement.CustomerContact).FontColor("#475569");
                    });

                    // Table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.8f);
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            static IContainer HeaderCell(IContainer c) =>
                                c.Background("#1e293b").Padding(6);

                            header.Cell().Element(HeaderCell).Text("Date").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).Text("Invoice #").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).Text("Payment Method").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Sub-Total").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("VAT").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Total").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Status").Bold().FontColor("#ffffff");
                        });

                        var alt = false;
                        foreach (var line in statement.Lines)
                        {
                            var bg = alt ? "#f8fafc" : "#ffffff";
                            alt = !alt;

                            IContainer Cell(IContainer c) => c.Background(bg).Padding(6).BorderBottom(1).BorderColor("#f1f5f9");

                            table.Cell().Element(Cell).Text(line.Date.ToString("dd/MM/yyyy"));
                            table.Cell().Element(Cell).Text(line.InvoiceId.Substring(0, Math.Min(8, line.InvoiceId.Length)) + "...");
                            table.Cell().Element(Cell).Text(line.PaymentType ?? "-");
                            table.Cell().Element(Cell).AlignRight().Text("R " + line.SubTotal.ToString("N2"));
                            table.Cell().Element(Cell).AlignRight().Text("R " + line.VatTotal.ToString("N2"));
                            table.Cell().Element(Cell).AlignRight().Text("R " + line.GrandTotal.ToString("N2")).Bold();
                            table.Cell().Element(Cell).AlignCenter().Text(line.PaymentStatus ?? "-");
                        }

                        if (statement.Lines.Count == 0)
                        {
                            IContainer EmptyCell(IContainer c) => c.Background("#ffffff").Padding(12);
                            table.Cell().ColumnSpan(7).Element(EmptyCell)
                                .AlignCenter().Text("No invoices found for this period").FontColor("#94a3b8");
                        }
                    });

                    // Totals
                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem();
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total Invoiced").FontColor("#475569");
                                r.RelativeItem().AlignRight().Text("R " + statement.TotalGrandTotal.ToString("N2"));
                            });
                            c.Item().PaddingTop(4).LineHorizontal(1).LineColor("#e2e8f0");
                            c.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Total Paid").FontColor("#166534");
                                r.RelativeItem().AlignRight().Text("R " + statement.TotalPaid.ToString("N2")).FontColor("#166534");
                            });
                            c.Item().Row(r =>
                            {
                                var color = statement.TotalOutstanding > 0 ? "#dc2626" : "#166534";
                                r.RelativeItem().Text("Outstanding").Bold().FontColor(color);
                                r.RelativeItem().AlignRight().Text("R " + statement.TotalOutstanding.ToString("N2")).Bold().FontColor(color);
                            });
                        });
                    });
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span("This statement was generated automatically. Please contact KwaWicks if you have any queries.  |  kwawicks.co.za")
                        .FontSize(9).FontColor("#94a3b8");
                });
            });
        });

        var bytes = doc.GeneratePdf();
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateClientCreditStatementPdfAsync(
        ClientCreditStatementResponse statement,
        CancellationToken ct = default)
    {
        var periodLabel = statement.From.HasValue || statement.To.HasValue
            ? (statement.From.HasValue ? statement.From.Value.ToString("dd/MM/yyyy") : "—")
              + " to "
              + (statement.To.HasValue ? statement.To.Value.ToString("dd/MM/yyyy") : "—")
            : "All time";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ── Header ─────────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("KwaWicks").Bold().FontSize(24).FontColor("#166534");
                            c.Item().Text("ACCOUNT STATEMENT").Bold().FontSize(14).FontColor("#64748b");
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Generated: " + statement.GeneratedAt.ToString("dd/MM/yyyy")).FontColor("#475569");
                            c.Item().Text("Period: " + periodLabel).FontColor("#475569");
                        });
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(2).LineColor("#166534");

                    // ── Client info ────────────────────────────────────────
                    col.Item().PaddingBottom(16).Column(c =>
                    {
                        c.Item().Text("Account:").Bold().FontSize(10).FontColor("#64748b");
                        c.Item().Text(statement.CustomerName).Bold().FontSize(13);
                        if (!string.IsNullOrWhiteSpace(statement.CustomerAddress))
                            c.Item().Text(statement.CustomerAddress).FontColor("#475569");
                        if (!string.IsNullOrWhiteSpace(statement.CustomerContact))
                            c.Item().Text(statement.CustomerContact).FontColor("#475569");
                    });

                    // ── Opening balance ────────────────────────────────────
                    if (statement.From.HasValue)
                    {
                        col.Item().PaddingBottom(8).Row(row =>
                        {
                            row.RelativeItem().Text("Opening balance (before " + statement.From.Value.ToString("dd/MM/yyyy") + ")")
                                .FontColor("#475569");
                            var obColor = statement.OpeningBalance < 0 ? "#dc2626"
                                        : statement.OpeningBalance > 0 ? "#166534"
                                        : "#475569";
                            row.ConstantItem(120).AlignRight()
                                .Text("R " + statement.OpeningBalance.ToString("N2"))
                                .FontColor(obColor).Bold();
                        });
                        col.Item().PaddingBottom(8).LineHorizontal(1).LineColor("#e2e8f0");
                    }

                    // ── Ledger table ───────────────────────────────────────
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);    // Date
                            cols.RelativeColumn(1.4f);  // Type
                            cols.RelativeColumn(1f);    // Method
                            cols.RelativeColumn(3f);    // Description
                            cols.ConstantColumn(80);    // Amount
                            cols.ConstantColumn(90);    // Balance
                        });

                        table.Header(header =>
                        {
                            static IContainer HeaderCell(IContainer c) =>
                                c.Background("#1e293b").Padding(6);

                            header.Cell().Element(HeaderCell).Text("Date").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).Text("Type").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).Text("Method").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor("#ffffff");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Balance").Bold().FontColor("#ffffff");
                        });

                        if (statement.Lines.Count == 0)
                        {
                            IContainer EmptyCell(IContainer c) => c.Background("#ffffff").Padding(12);
                            table.Cell().ColumnSpan(6).Element(EmptyCell)
                                .AlignCenter().Text("No transactions found for this period.").FontColor("#94a3b8");
                        }

                        var alt = false;
                        foreach (var line in statement.Lines)
                        {
                            var bg = alt ? "#f8fafc" : "#ffffff";
                            alt = !alt;

                            IContainer Cell(IContainer c) =>
                                c.Background(bg).Padding(6).BorderBottom(1).BorderColor("#f1f5f9");

                            // Amount: green for deposits, red for charges
                            var amtColor = line.Amount >= 0 ? "#166534" : "#dc2626";
                            var amtText  = line.Amount >= 0
                                ? "+ R " + line.Amount.ToString("N2")
                                : "− R " + Math.Abs(line.Amount).ToString("N2");

                            // Running balance colour
                            var balColor = line.RunningBalance < 0  ? "#dc2626"
                                         : line.RunningBalance > 0  ? "#166534"
                                         : "#475569";

                            // Human-readable type label
                            var typeLabel = line.EntryType switch
                            {
                                "Deposit"          => "Payment",
                                "InvoiceCharge"    => "Invoice Charge",
                                "ManualAdjustment" => "Adjustment",
                                _                  => line.EntryType
                            };

                            // Tag bank-recon originated deposits
                            var desc = line.Description;
                            if (line.CreatedByUserId == "BankRecon" && !desc.Contains("Bank statement"))
                                desc = "[Bank Recon] " + desc;

                            table.Cell().Element(Cell).Text(line.Date.ToString("dd/MM/yy"));
                            table.Cell().Element(Cell).Text(typeLabel);
                            table.Cell().Element(Cell).Text(line.PaymentMethod);
                            table.Cell().Element(Cell).Text(desc).FontColor("#374151");
                            table.Cell().Element(Cell).AlignRight().Text(amtText).FontColor(amtColor).Bold();
                            table.Cell().Element(Cell).AlignRight().Text("R " + line.RunningBalance.ToString("N2")).FontColor(balColor).Bold();
                        }
                    });

                    // ── Summary footer ─────────────────────────────────────
                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(300).Column(c =>
                        {
                            if (statement.From.HasValue)
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Opening Balance").FontColor("#475569");
                                    r.ConstantItem(110).AlignRight().Text("R " + statement.OpeningBalance.ToString("N2")).FontColor("#475569");
                                });
                            }

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total Payments Received").FontColor("#166534");
                                r.ConstantItem(110).AlignRight().Text("R " + statement.TotalDeposits.ToString("N2")).FontColor("#166534");
                            });

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total Charges").FontColor("#dc2626");
                                r.ConstantItem(110).AlignRight().Text("R " + statement.TotalCharges.ToString("N2")).FontColor("#dc2626");
                            });

                            c.Item().PaddingTop(6).LineHorizontal(2).LineColor("#1e293b");

                            var closingColor = statement.ClosingBalance < 0  ? "#dc2626"
                                             : statement.ClosingBalance > 0  ? "#166534"
                                             : "#475569";

                            var closingLabel = statement.ClosingBalance < 0
                                ? "Amount Outstanding"
                                : statement.ClosingBalance > 0
                                    ? "Credit Available"
                                    : "Account Settled";

                            c.Item().PaddingTop(6).Row(r =>
                            {
                                r.RelativeItem().Text(closingLabel).Bold().FontSize(12).FontColor(closingColor);
                                r.ConstantItem(110).AlignRight()
                                    .Text("R " + Math.Abs(statement.ClosingBalance).ToString("N2"))
                                    .Bold().FontSize(12).FontColor(closingColor);
                            });
                        });
                    });
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span("This statement was generated automatically. Please contact KwaWicks if you have any queries.  |  kwawicks.co.za")
                        .FontSize(9).FontColor("#94a3b8");
                });
            });
        });

        var bytes = doc.GeneratePdf();
        return Task.FromResult(bytes);
    }
}
