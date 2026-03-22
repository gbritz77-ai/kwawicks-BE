using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IPdfService
{
    Task<byte[]> GenerateInvoicePdfAsync(
        InvoiceResponse invoice,
        ClientDto client,
        IEnumerable<(string speciesId, string speciesName)> speciesNames,
        CancellationToken ct = default);

    Task<byte[]> GenerateStatementPdfAsync(
        CustomerStatementResponse statement,
        CancellationToken ct = default);
}
