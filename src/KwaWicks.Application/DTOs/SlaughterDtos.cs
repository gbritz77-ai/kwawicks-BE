namespace KwaWicks.Application.DTOs;

// ── Request ──────────────────────────────────────────────────────────────────

public class CreateSlaughterRequest
{
    public string SourceSpeciesId { get; set; } = default!;
    public int SourceQty { get; set; }
    /// <summary>Cost per source unit (e.g. cost of one rooster). Defaults to species.UnitCost if omitted.</summary>
    public decimal? SourceUnitCost { get; set; }
    public List<SlaughterYieldLineRequest> Yields { get; set; } = new();
    public string? Notes { get; set; }
}

public class SlaughterYieldLineRequest
{
    public string SpeciesId { get; set; } = default!;
    public int Qty { get; set; }
    /// <summary>Cost price of this yield item (used to update species.UnitCost).</summary>
    public decimal UnitCost { get; set; }
    /// <summary>Selling price of this yield item (used to update species.SellPrice).</summary>
    public decimal UnitPrice { get; set; }
}

// ── Response ─────────────────────────────────────────────────────────────────

public class SlaughterBatchResponse
{
    public string BatchId { get; set; } = default!;
    public DateTime SlaughteredAtUtc { get; set; }
    public string SourceSpeciesId { get; set; } = default!;
    public string SourceSpeciesName { get; set; } = default!;
    public int SourceQty { get; set; }
    public decimal SourceUnitCost { get; set; }
    public decimal TotalInputCost { get; set; }
    public string? Notes { get; set; }
    public List<SlaughterYieldLineResponse> Yields { get; set; } = new();
}

public class SlaughterYieldLineResponse
{
    public string SpeciesId { get; set; } = default!;
    public string SpeciesName { get; set; } = default!;
    public int Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
}
