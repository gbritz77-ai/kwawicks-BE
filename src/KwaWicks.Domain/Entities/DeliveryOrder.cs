namespace KwaWicks.Domain.Entities;

public class DeliveryOrder
{
    public string DeliveryOrderId { get; set; } = Guid.NewGuid().ToString();
    public string InvoiceId { get; set; } = "";
    public string HubId { get; set; } = "";
    public string CustomerId { get; set; } = "";

    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";

    public string Status { get; set; } = "Open";
    // Open, OutForDelivery, Delivered

    public string DeliveryAddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";

    public List<DeliveryOrderLine> Lines { get; set; } = new();

    /// <summary>Driver has submitted the leftover (not-wanted) stock for hub check-in.</summary>
    public bool ReturnSubmitted { get; set; } = false;

    /// <summary>Hub staff has physically verified and checked in the returned stock.</summary>
    public bool ReturnCheckedIn { get; set; } = false;

    /// <summary>Expected cash from the driver (set when invoice is linked: Cash = GrandTotal, Split = Cash leg).</summary>
    public decimal? CashExpected { get; set; }

    /// <summary>Cash amount hub staff received from the driver on return (null = not yet confirmed).</summary>
    public decimal? CashReceived { get; set; }

    /// <summary>True once hub has confirmed the cash amount returned by the driver.</summary>
    public bool CashConfirmed { get; set; }

    /// <summary>Difference between CashReceived and CashExpected (positive = over, negative = short).</summary>
    public decimal? CashDiscrepancy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class DeliveryOrderLine
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Populated when driver completes delivery
    public int DeliveredQty { get; set; }
    public int TotalReturnedQty { get; set; }

    /// <summary>How many the driver physically returned to hub (submitted for check-in).</summary>
    public int ReturnedToHubQty { get; set; }

    // Populated by admin after delivery (returns inspection)
    public bool ReturnsInspected { get; set; }
    public int InspectedDeadQty { get; set; }
    public int InspectedMutilatedQty { get; set; }
}
