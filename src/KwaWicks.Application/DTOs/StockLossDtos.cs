namespace KwaWicks.Application.DTOs;

public class RecordStockLossRequest
{
    public string SpeciesId { get; set; } = "";

    /// <summary>Number of units that died. Must be > 0.</summary>
    public int Qty { get; set; }

    public string Notes { get; set; } = "";
}

public class StockLossResponse
{
    public string LossId { get; set; } = "";
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public int Qty { get; set; }
    public string Notes { get; set; } = "";
    public string RecordedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    /// <summary>QtyOnHandHub after this loss was applied.</summary>
    public int QtyOnHandHubAfter { get; set; }
}
