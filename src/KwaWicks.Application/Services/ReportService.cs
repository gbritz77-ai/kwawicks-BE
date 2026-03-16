using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Application.Services;

public class ReportService : IReportService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IDeliveryOrderRepository _deliveryOrders;
    private readonly IClientRepository _clients;

    public ReportService(IInvoiceRepository invoices, IDeliveryOrderRepository deliveryOrders, IClientRepository clients)
    {
        _invoices = invoices;
        _deliveryOrders = deliveryOrders;
        _clients = clients;
    }

    public async Task<RevenueSummaryResponse> GetRevenueSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var all = await _invoices.ListAsync(null, null, ct);

        var filtered = all
            .Where(i => from == null || i.CreatedAt >= from.Value)
            .Where(i => to == null || i.CreatedAt <= to.Value.AddDays(1))
            .ToList();

        var byType = filtered
            .GroupBy(i => string.IsNullOrEmpty(i.PaymentType) ? "Unknown" : i.PaymentType)
            .Select(g => new PaymentTypeBreakdown
            {
                PaymentType = g.Key,
                Count = g.Count(),
                SubTotal = g.Sum(i => i.SubTotal),
                GrandTotal = g.Sum(i => i.GrandTotal)
            })
            .OrderBy(b => b.PaymentType)
            .ToList();

        return new RevenueSummaryResponse
        {
            From = from,
            To = to,
            TotalInvoices = filtered.Count,
            TotalSubTotal = filtered.Sum(i => i.SubTotal),
            TotalVat = filtered.Sum(i => i.VatTotal),
            TotalGrandTotal = filtered.Sum(i => i.GrandTotal),
            ByPaymentType = byType
        };
    }

    public async Task<OutstandingPaymentsResponse> GetOutstandingPaymentsAsync(CancellationToken ct = default)
    {
        var all = await _invoices.ListAsync(null, null, ct);

        var outstanding = all
            .Where(i => i.PaymentStatus == "Pending" && (i.PaymentType == "EFT" || i.PaymentType == "Credit"))
            .OrderBy(i => i.CreatedAt)
            .Select(i => new OutstandingPaymentItem
            {
                InvoiceId = i.InvoiceId,
                CustomerId = i.CustomerId,
                PaymentType = i.PaymentType,
                GrandTotal = i.GrandTotal,
                CreatedAt = i.CreatedAt,
                DaysOutstanding = (int)(DateTime.UtcNow - i.CreatedAt).TotalDays
            })
            .ToList();

        return new OutstandingPaymentsResponse
        {
            Count = outstanding.Count,
            TotalOutstanding = outstanding.Sum(o => o.GrandTotal),
            Items = outstanding
        };
    }

    public async Task<DriverPerformanceResponse> GetDriverPerformanceAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var orders = await _deliveryOrders.ListAsync(null, null, "Delivered", ct);

        var filtered = orders
            .Where(o => from == null || o.UpdatedAt >= from.Value)
            .Where(o => to == null || o.UpdatedAt <= to.Value.AddDays(1))
            .ToList();

        // Build invoice lookup by DeliveryOrderId for total values
        var invoices = await _invoices.ListAsync(null, null, ct);
        var invoiceByDo = invoices
            .Where(i => !string.IsNullOrEmpty(i.DeliveryOrderId))
            .ToDictionary(i => i.DeliveryOrderId, i => i);

        var drivers = filtered
            .GroupBy(o => o.AssignedDriverId)
            .Select(g =>
            {
                var totalValue = g
                    .Where(o => invoiceByDo.ContainsKey(o.DeliveryOrderId))
                    .Sum(o => invoiceByDo[o.DeliveryOrderId].GrandTotal);

                return new DriverPerformanceItem
                {
                    DriverId = g.Key,
                    DriverName = g.First().AssignedDriverName,
                    DeliveriesCompleted = g.Count(),
                    TotalValue = totalValue,
                    TotalDeadReturns = g.SelectMany(o => o.Lines).Sum(l => l.ReturnedDeadQty),
                    TotalMutilatedReturns = g.SelectMany(o => o.Lines).Sum(l => l.ReturnedMutilatedQty),
                    TotalNotWantedReturns = g.SelectMany(o => o.Lines).Sum(l => l.ReturnedNotWantedQty)
                };
            })
            .OrderByDescending(d => d.TotalValue)
            .ToList();

        return new DriverPerformanceResponse { From = from, To = to, Drivers = drivers };
    }

    public async Task<ReturnsSummaryResponse> GetReturnsSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var orders = await _deliveryOrders.ListAsync(null, null, "Delivered", ct);

        var filtered = orders
            .Where(o => from == null || o.UpdatedAt >= from.Value)
            .Where(o => to == null || o.UpdatedAt <= to.Value.AddDays(1))
            .ToList();

        var items = filtered
            .SelectMany(o => o.Lines)
            .Where(l => l.ReturnedDeadQty > 0 || l.ReturnedMutilatedQty > 0 || l.ReturnedNotWantedQty > 0)
            .GroupBy(l => l.SpeciesId)
            .Select(g => new ReturnsSummaryItem
            {
                SpeciesId = g.Key,
                DeadQty = g.Sum(l => l.ReturnedDeadQty),
                MutilatedQty = g.Sum(l => l.ReturnedMutilatedQty),
                NotWantedQty = g.Sum(l => l.ReturnedNotWantedQty),
                TotalReturns = g.Sum(l => l.ReturnedDeadQty + l.ReturnedMutilatedQty + l.ReturnedNotWantedQty)
            })
            .OrderByDescending(i => i.TotalReturns)
            .ToList();

        return new ReturnsSummaryResponse { From = from, To = to, Items = items };
    }

    public async Task<DeliveryStatusSummaryResponse> GetDeliveryStatusSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var all = await _deliveryOrders.ListAsync(null, null, null, ct);

        var filtered = all
            .Where(o => from == null || o.CreatedAt >= from.Value)
            .Where(o => to == null || o.CreatedAt <= to.Value.AddDays(1))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        var invoices = await _invoices.ListAsync(null, null, ct);
        var invoiceByDo = invoices
            .Where(i => !string.IsNullOrEmpty(i.DeliveryOrderId))
            .ToDictionary(i => i.DeliveryOrderId, i => i);

        var clients = await _clients.ListAsync(1000, ct);
        var clientById = clients.ToDictionary(c => c.ClientId, c => c.ClientName);

        var items = filtered.Select(o =>
        {
            invoiceByDo.TryGetValue(o.DeliveryOrderId, out var inv);
            clientById.TryGetValue(o.CustomerId, out var clientName);
            return new DeliveryStatusItem
            {
                DeliveryOrderId = o.DeliveryOrderId,
                Status = o.Status,
                CustomerId = o.CustomerId,
                CustomerName = clientName ?? o.CustomerId,
                DriverName = o.AssignedDriverName,
                DeliveryAddress = $"{o.DeliveryAddressLine1}, {o.City}",
                TotalItems = o.Lines.Sum(l => l.Quantity),
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                InvoiceId = inv?.InvoiceId ?? "",
                PaymentType = inv?.PaymentType ?? "",
                PaymentStatus = inv?.PaymentStatus ?? "",
                GrandTotal = inv?.GrandTotal ?? 0m
            };
        }).ToList();

        return new DeliveryStatusSummaryResponse
        {
            From = from,
            To = to,
            OpenCount = items.Count(i => i.Status == "Open"),
            InTransitCount = items.Count(i => i.Status == "OutForDelivery"),
            DeliveredCount = items.Count(i => i.Status == "Delivered"),
            Orders = items
        };
    }

    public async Task<List<InvoiceResponse>> GetInvoicesAsync(string? customerId, string? paymentStatus, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var all = await _invoices.ListAsync(null, customerId, ct);

        return all
            .Where(i => string.IsNullOrEmpty(paymentStatus) || i.PaymentStatus == paymentStatus)
            .Where(i => from == null || i.CreatedAt >= from.Value)
            .Where(i => to == null || i.CreatedAt <= to.Value.AddDays(1))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceResponse
            {
                InvoiceId = i.InvoiceId,
                CustomerId = i.CustomerId,
                HubId = i.HubId,
                DeliveryOrderId = i.DeliveryOrderId,
                CreatedByDriverId = i.CreatedByDriverId,
                Status = i.Status,
                PaymentType = i.PaymentType,
                PaymentStatus = i.PaymentStatus,
                ReceiptS3Key = i.ReceiptS3Key,
                SubTotal = i.SubTotal,
                VatTotal = i.VatTotal,
                GrandTotal = i.GrandTotal,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                Lines = i.Lines.Select(l => new InvoiceLineResponse
                {
                    SpeciesId = l.SpeciesId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    LineTotal = l.LineTotal
                }).ToList()
            })
            .ToList();
    }

    public async Task<CustomerStatementResponse> GetCustomerStatementAsync(string customerId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var client = await _clients.GetAsync(customerId, ct)
            ?? throw new InvalidOperationException($"Customer not found: {customerId}");

        var allInvoices = await _invoices.ListAsync(null, customerId, ct);

        var filtered = allInvoices
            .Where(i => from == null || i.CreatedAt >= from.Value)
            .Where(i => to == null || i.CreatedAt <= to.Value.AddDays(1))
            .OrderBy(i => i.CreatedAt)
            .ToList();

        var lines = filtered.Select(i => new CustomerStatementLine
        {
            InvoiceId = i.InvoiceId,
            Date = i.CreatedAt,
            PaymentType = i.PaymentType,
            PaymentStatus = i.PaymentStatus,
            SubTotal = i.SubTotal,
            VatTotal = i.VatTotal,
            GrandTotal = i.GrandTotal
        }).ToList();

        var totalGrand = lines.Sum(l => l.GrandTotal);
        var totalPaid  = lines.Where(l => l.PaymentStatus == "Paid").Sum(l => l.GrandTotal);

        return new CustomerStatementResponse
        {
            CustomerId = client.ClientId,
            CustomerName = client.ClientName,
            CustomerAddress = client.ClientAddress,
            CustomerContact = client.ClientContactDetails,
            From = from,
            To = to,
            GeneratedAt = DateTime.UtcNow,
            Lines = lines,
            TotalSubTotal = lines.Sum(l => l.SubTotal),
            TotalVat = lines.Sum(l => l.VatTotal),
            TotalGrandTotal = totalGrand,
            TotalPaid = totalPaid,
            TotalOutstanding = totalGrand - totalPaid
        };
    }

    public async Task<List<MyDeliveryItem>> GetMyDeliveriesAsync(string driverId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var orders = await _deliveryOrders.ListAsync(driverId, null, "Delivered", ct);

        var filtered = orders
            .Where(o => from == null || o.UpdatedAt >= from.Value)
            .Where(o => to == null || o.UpdatedAt <= to.Value.AddDays(1))
            .ToList();

        var invoices = await _invoices.ListAsync(null, null, ct);
        var invoiceByDo = invoices
            .Where(i => !string.IsNullOrEmpty(i.DeliveryOrderId))
            .ToDictionary(i => i.DeliveryOrderId, i => i);

        return filtered
            .OrderByDescending(o => o.UpdatedAt)
            .Select(o =>
            {
                invoiceByDo.TryGetValue(o.DeliveryOrderId, out var inv);
                return new MyDeliveryItem
                {
                    DeliveryOrderId = o.DeliveryOrderId,
                    InvoiceId = inv?.InvoiceId ?? "",
                    CustomerId = o.CustomerId,
                    DeliveryAddress = $"{o.DeliveryAddressLine1}, {o.City}",
                    CompletedAt = o.UpdatedAt,
                    GrandTotal = inv?.GrandTotal ?? 0m,
                    PaymentType = inv?.PaymentType ?? "",
                    PaymentStatus = inv?.PaymentStatus ?? ""
                };
            })
            .ToList();
    }
}
