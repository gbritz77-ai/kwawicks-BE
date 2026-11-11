using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IBankTransactionService
{
    Task<(string UploadUrl, string S3Key)> GetUploadUrlAsync(string fileName, CancellationToken ct);
    Task<ImportTransactionResult> ImportAsync(string s3Key, string fileName, CancellationToken ct);
    Task<FlatTransactionListResponse> ListAsync(string? type, string? status, string? search, DateTime? from, DateTime? to, CancellationToken ct);
    Task<AllocateTransactionResponse> AllocateAsync(string transactionId, AllocateBankTransactionRequest request, CancellationToken ct);
    Task<AllocateTransactionResponse> AllocateNonClientAsync(string transactionId, AllocateNonClientRequest request, CancellationToken ct);
    Task<AllocateTransactionResponse> AllocateSupplierAsync(string transactionId, AllocateSupplierRequest request, CancellationToken ct);
    Task<AllocateTransactionResponse> AllocateClientCreditAsync(string transactionId, AllocateClientCreditRequest request, CancellationToken ct);
    Task<AllocateTransactionResponse> SplitClientCreditAsync(string transactionId, SplitClientCreditRequest request, CancellationToken ct);
    Task<AllocateTransactionResponse> AllocateExpenseAsync(string transactionId, AllocateExpenseRequest request, CancellationToken ct);
    Task<AllocateTransactionResponse> AllocatePettyCashAsync(string transactionId, CancellationToken ct);
    Task<BankTransactionResponse> DeallocateAsync(string transactionId, CancellationToken ct);
    Task<MigrateResult> MigrateFromStatementsAsync(CancellationToken ct);
    Task<List<BankReconAllocationReportItem>> GetAllocationReportAsync(DateTime? from, DateTime? to, CancellationToken ct);
    Task<DebitReportResponse> GetDebitReportAsync(DateTime? from, DateTime? to, CancellationToken ct);
    Task<List<string>> GetExpenseCategoriesAsync(CancellationToken ct);
    Task<List<string>> AddExpenseCategoryAsync(string category, CancellationToken ct);
}
