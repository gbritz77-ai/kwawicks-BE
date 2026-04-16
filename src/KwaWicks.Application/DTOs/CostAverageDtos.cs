namespace KwaWicks.Application.DTOs;

// ── Request ───────────────────────────────────────────────────────────────────

public class CalculateCostAverageRequest
{
    /// <summary>Four-digit year, e.g. 2026</summary>
    public int Year { get; set; }

    /// <summary>Month number 1–12</summary>
    public int Month { get; set; }

    /// <summary>When true, species.UnitCost is updated to the calculated average.</summary>
    public bool ApplyToSpecies { get; set; } = true;
}

// ── Response ──────────────────────────────────────────────────────────────────

public class CostAverageRecordResponse
{
    public string SpeciesId { get; set; } = default!;
    public string SpeciesName { get; set; } = default!;
    public string Month { get; set; } = default!;
    public int TotalQty { get; set; }
    public decimal AvgCostExVat { get; set; }
    public decimal AvgCostIncVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal PriorUnitCost { get; set; }
    public decimal CostDelta { get; set; }   // AvgCostExVat - PriorUnitCost
    public bool AppliedToSpecies { get; set; }
    public DateTime CalculatedAt { get; set; }
    public List<CostContributionLineResponse> Sources { get; set; } = new();
}

public class CostContributionLineResponse
{
    public string Source { get; set; } = default!;
    public string SourceId { get; set; } = default!;
    public string SourceRef { get; set; } = "";
    public DateTime Date { get; set; }
    public int Qty { get; set; }
    public decimal UnitCostExVat { get; set; }
}
