namespace KwaWicks.Application.DTOs;

public class CreateSpeciesRequest
{
    public string Name { get; set; } = "";
    public decimal UnitCost { get; set; }
    public decimal? SellPrice { get; set; }

    // ✅ NEW
    public decimal Vat { get; set; }
    public int QtyOnHandHub { get; set; }
    public int QtyBookedOutForDelivery { get; set; }
}

public class UpdateSpeciesRequest
{
    public string Name { get; set; } = "";
    public decimal UnitCost { get; set; }
    public decimal? SellPrice { get; set; }
    public bool IsActive { get; set; } = true;

    // ✅ NEW
    public decimal Vat { get; set; }
    public int QtyOnHandHub { get; set; }
    public int QtyBookedOutForDelivery { get; set; }
}

public class SpeciesResponse
{
    public string SpeciesId { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal UnitCost { get; set; }
    public decimal? SellPrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // ✅ NEW
    public decimal Vat { get; set; }
    public int QtyOnHandHub { get; set; }
    public int QtyBookedOutForDelivery { get; set; }

    // ✅ Derived (optional – can also compute in UI)
    public int QtyAvailable => QtyOnHandHub - QtyBookedOutForDelivery;
}