namespace KwaWicks.Domain.Entities;

public class SlaughterBatch
{
    public string BatchId { get; set; } = default!;
    public DateTime SlaughteredAtUtc { get; set; } = DateTime.UtcNow;

    // Source (what was slaughtered)
    public string SourceSpeciesId { get; set; } = default!;
    public string SourceSpeciesName { get; set; } = default!;
    public int SourceQty { get; set; }
    public decimal SourceUnitCost { get; set; }
    public decimal TotalInputCost => SourceQty * SourceUnitCost;

    // Yield items produced by the slaughter
    public List<SlaughterYieldLine> Yields { get; set; } = new();

    public string? Notes { get; set; }
    public string? CreatedByUserId { get; set; }
}

public class SlaughterYieldLine
{
    public string SpeciesId { get; set; } = default!;
    public string SpeciesName { get; set; } = default!;
    public int Qty { get; set; }
    public decimal UnitCost { get; set; }   // cost price set by admin
    public decimal UnitPrice { get; set; }  // selling price set by admin
}
