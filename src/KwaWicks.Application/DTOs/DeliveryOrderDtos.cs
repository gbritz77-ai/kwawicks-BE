namespace KwaWicks.Application.DTOs;

public class CreateDeliveryOrderRequest
{
    public string CustomerId { get; set; } = "";
    public string HubId { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";

    public string DeliveryAddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";

    public List<CreateDeliveryOrderLine> Lines { get; set; } = new();
}

public class CreateDeliveryOrderLine
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
}

public class UpdateDeliveryStatusRequest
{
    public string Status { get; set; } = "";
}

public class DeliveryOrderResponse
{
    public string DeliveryOrderId { get; set; } = "";
    public string InvoiceId { get; set; } = "";
    public string HubId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string AssignedDriverName { get; set; } = "";
    public string Status { get; set; } = "";
    public string DeliveryAddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public List<DeliveryOrderLineResponse> Lines { get; set; } = new();
    public bool ReturnSubmitted { get; set; }
    public bool ReturnCheckedIn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DeliveryOrderLineResponse
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int DeliveredQty { get; set; }
    public int ReturnedDeadQty { get; set; }
    public int ReturnedMutilatedQty { get; set; }
    public int ReturnedNotWantedQty { get; set; }
    public int ReturnedToHubQty { get; set; }
}

public class SubmitReturnRequest
{
    public List<SubmitReturnLine> Lines { get; set; } = new();
}

public class SubmitReturnLine
{
    public string SpeciesId { get; set; } = "";
    public int Qty { get; set; }
}

// Admin: edit qty / price on an Open delivery order
public class EditDeliveryOrderLinesRequest
{
    public List<EditDeliveryOrderLine> Lines { get; set; } = new();
}

public class EditDeliveryOrderLine
{
    public string SpeciesId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class DriverStockItem
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int AvailableQty { get; set; }
    public decimal UnitPrice { get; set; }
}
