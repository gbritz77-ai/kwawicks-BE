namespace KwaWicks.Application.DTOs;

public class CreateProcurementOrderRequest
{
    public string SupplierId { get; set; } = "";
    public string SupplierOrderReference { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<CreateProcurementOrderLine> Lines { get; set; } = new();
}

public class CreateProcurementOrderLine
{
    public string SpeciesId { get; set; } = "";
    public int OrderedQty { get; set; }
    public decimal? UnitCost { get; set; }
}

public class ProcurementOrderResponse
{
    public string ProcurementOrderId { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string SupplierOrderReference { get; set; } = "";
    public string Status { get; set; } = "";
    public string Notes { get; set; } = "";
    public string InvoiceS3Key { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public List<ProcurementOrderLineResponse> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProcurementOrderLineResponse
{
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int OrderedQty { get; set; }
    public decimal UnitCost { get; set; }
}

public class ProcurementInvoiceUploadUrlResponse
{
    public string UploadUrl { get; set; } = "";
    public string S3Key { get; set; } = "";
}
