using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IReportService
{
    Task<RevenueSummaryResponse> GetRevenueSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<OutstandingPaymentsResponse> GetOutstandingPaymentsAsync(CancellationToken ct = default);
    Task<DriverPerformanceResponse> GetDriverPerformanceAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<ReturnsSummaryResponse> GetReturnsSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<DeliveryStatusSummaryResponse> GetDeliveryStatusSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<InvoiceResponse>> GetInvoicesAsync(string? customerId, string? paymentStatus, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<MyDeliveryItem>> GetMyDeliveriesAsync(string driverId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<CustomerStatementResponse> GetCustomerStatementAsync(string customerId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<CustomerStatementResponse>> GetAllCustomerStatementsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<SpeciesRevenueResponse> GetSpeciesRevenueAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<ClientCreditStatementResponse> GetClientCreditStatementAsync(string clientId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<SalesReportResponse> GetSalesReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>Admin/Finance: stock taken by staff on their hub account, for salary deduction.</summary>
    Task<StaffStockDeductionsReportResponse> GetStaffStockDeductionsAsync(
        string? staffMemberId, DateTime? from, DateTime? to, CancellationToken ct = default);
}
