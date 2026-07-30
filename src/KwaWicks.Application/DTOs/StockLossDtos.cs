namespace KwaWicks.Application.DTOs;

public class RecordStockLossRequest
{
    public string SpeciesId { get; set; } = "";

    /// <summary>Number of units. Must be > 0.</summary>
    public int Qty { get; set; }

    /// <summary>Over = surplus found (adds to stock). Under = loss/missing (removes from stock). Defaults to Under.</summary>
    public string AdjustmentType { get; set; } = "Under";

    public string Notes { get; set; } = "";
}

public class StockLossResponse
{
    public string LossId { get; set; } = "";
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public string AdjustmentType { get; set; } = "Under";
    public string Notes { get; set; } = "";
    public string RecordedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    /// <summary>QtyOnHandHub after this adjustment was applied.</summary>
    public int QtyOnHandHubAfter { get; set; }
}
