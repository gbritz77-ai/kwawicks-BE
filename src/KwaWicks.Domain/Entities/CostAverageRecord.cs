namespace KwaWicks.Domain.Entities;

/// <summary>
/// Monthly weighted-average cost record per species.
/// PK = COSTAVG#{SpeciesId}, SK = MONTH#{YYYY-MM}
/// </summary>
public class CostAverageRecord
{
    public string SpeciesId { get; set; } = default!;
    public string SpeciesName { get; set; } = default!;

    /// <summary>Calendar month in YYYY-MM format.</summary>
    public string Month { get; set; } = default!;

    public int TotalQty { get; set; }

    public decimal AvgCostExVat { get; set; }
    public decimal AvgCostIncVat { get; set; }
    public decimal VatRate { get; set; }

    /// <summary>Snapshot of species.UnitCost before this record was applied.</summary>
    public decimal PriorUnitCost { get; set; }

    /// <summary>Whether species.UnitCost was updated to AvgCostExVat when this was calculated.</summary>
    public bool AppliedToSpecies { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Individual cost events that contributed to the average.</summary>
    public List<CostContributionLine> Sources { get; set; } = new();
}

public class CostContributionLine
{
    /// <summary>"ProcurementOrder" or "SlaughterBatch"</summary>
    public string Source { get; set; } = default!;
    public string SourceId { get; set; } = default!;
    public string SourceRef { get; set; } = "";   // e.g. PO reference or batch notes
    public DateTime Date { get; set; }
    public int Qty { get; set; }
    public decimal UnitCostExVat { get; set; }
}
