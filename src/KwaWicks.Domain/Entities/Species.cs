namespace KwaWicks.Domain.Entities;

public class Species
{
    public string SpeciesId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal UnitCost { get; set; }
    public decimal? SellPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal Vat { get; set; }                    // e.g. 0.15 for 15% (or store 15 - your choice)
    public int QtyOnHandHub { get; set; }               // stock at hub
    public int QtyBookedOutForDelivery { get; set; }    // allocated to deliveries
}
