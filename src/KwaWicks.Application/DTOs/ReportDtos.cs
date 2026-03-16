namespace KwaWicks.Application.DTOs;

// ── Admin: Revenue Summary ───────────────────────────────────────────────────
public class RevenueSummaryResponse
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int TotalInvoices { get; set; }
    public decimal TotalSubTotal { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGrandTotal { get; set; }
    public List<PaymentTypeBreakdown> ByPaymentType { get; set; } = new();
}

public class PaymentTypeBreakdown
{
    public string PaymentType { get; set; } = "";
    public int Count { get; set; }
    public decimal SubTotal { get; set; }
    public decimal GrandTotal { get; set; }
}

// ── Admin: Outstanding Payments ──────────────────────────────────────────────
public class OutstandingPaymentsResponse
{
    public int Count { get; set; }
    public decimal TotalOutstanding { get; set; }
    public List<OutstandingPaymentItem> Items { get; set; } = new();
}

public class OutstandingPaymentItem
{
    public string InvoiceId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DaysOutstanding { get; set; }
}

// ── Admin: Driver Performance ────────────────────────────────────────────────
public class DriverPerformanceResponse
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<DriverPerformanceItem> Drivers { get; set; } = new();
}

public class DriverPerformanceItem
{
    public string DriverId { get; set; } = "";
    public string DriverName { get; set; } = "";
    public int DeliveriesCompleted { get; set; }
    public decimal TotalValue { get; set; }
    public int TotalDeadReturns { get; set; }
    public int TotalMutilatedReturns { get; set; }
    public int TotalNotWantedReturns { get; set; }
}

// ── Admin: Returns Summary ───────────────────────────────────────────────────
public class ReturnsSummaryResponse
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<ReturnsSummaryItem> Items { get; set; } = new();
}

public class ReturnsSummaryItem
{
    public string SpeciesId { get; set; } = "";
    public int DeadQty { get; set; }
    public int MutilatedQty { get; set; }
    public int NotWantedQty { get; set; }
    public int TotalReturns { get; set; }
}

// ── Driver: My Delivery History ──────────────────────────────────────────────
public class MyDeliveryItem
{
    public string DeliveryOrderId { get; set; } = "";
    public string InvoiceId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
    public DateTime CompletedAt { get; set; }
    public decimal GrandTotal { get; set; }
    public string PaymentType { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
}
