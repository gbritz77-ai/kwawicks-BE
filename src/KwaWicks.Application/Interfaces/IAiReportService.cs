using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IAiReportService
{
    Task<AiReportResult> RunReportAsync(string prompt, CancellationToken ct);
}
