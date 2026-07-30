namespace KwaWicks.Domain.Entities;

public class StockLoss
{
    public string LossId { get; set; } = Guid.NewGuid().ToString("N");
    public string SpeciesId { get; set; } = "";
    public string SpeciesName { get; set; } = "";

    /// <summary>Number of units adjusted. Always positive — direction is in AdjustmentType.</summary>
    public int Qty { get; set; }

    /// <summary>Over = surplus/stock found (increases QtyOnHand). Under = loss/missing (decreases QtyOnHand).</summary>
    public string AdjustmentType { get; set; } = "Under";

    public string Notes { get; set; } = "";
    public string RecordedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
