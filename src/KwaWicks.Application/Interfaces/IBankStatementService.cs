using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IBankStatementService
{
    Task<(string UploadUrl, string S3Key)> GetUploadUrlAsync(string fileName, CancellationToken ct);
    Task<BankStatementResponse> ProcessUploadAsync(ProcessBankStatementRequest request, CancellationToken ct);
    Task<List<BankStatementSummaryResponse>> ListAsync(CancellationToken ct);
    Task<BankStatementResponse?> GetAsync(string statementId, CancellationToken ct);
    Task<BankStatementResponse> AllocateAsync(string statementId, string transactionId, AllocateBankTransactionRequest request, CancellationToken ct);
    Task<BankStatementResponse> DeallocateAsync(string statementId, string transactionId, CancellationToken ct);
}
