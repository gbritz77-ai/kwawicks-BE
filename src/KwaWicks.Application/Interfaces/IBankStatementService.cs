using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IBankStatementService
{
    Task<(string UploadUrl, string S3Key)> GetUploadUrlAsync(string fileName, CancellationToken ct);
    Task<BankStatementResponse> ProcessUploadAsync(ProcessBankStatementRequest request, CancellationToken ct);
    Task<List<BankStatementSummaryResponse>> ListAsync(CancellationToken ct);
    Task<BankStatementResponse?> GetAsync(string statementId, CancellationToken ct, string? search = null, decimal? amount = null);
    Task<AllocateResponse> AllocateAsync(string statementId, string transactionId, AllocateBankTransactionRequest request, CancellationToken ct);
    Task<AllocateResponse> AllocateNonClientAsync(string statementId, string transactionId, AllocateNonClientRequest request, CancellationToken ct);
    Task<AllocateResponse> AllocateSupplierAsync(string statementId, string transactionId, AllocateSupplierRequest request, CancellationToken ct);
    Task<AllocateResponse> AllocateClientCreditAsync(string statementId, string transactionId, AllocateClientCreditRequest request, CancellationToken ct);
    Task<BankStatementResponse> DeallocateAsync(string statementId, string transactionId, CancellationToken ct);
    Task<List<BankReconAllocationReportItem>> GetAllocationReportAsync(DateTime? from, DateTime? to, CancellationToken ct);
}
