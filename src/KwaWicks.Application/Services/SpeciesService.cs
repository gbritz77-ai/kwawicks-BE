using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class SpeciesService
{
    private readonly ISpeciesRepository _repo;

    public SpeciesService(ISpeciesRepository repo)
    {
        _repo = repo;
    }

    public async Task<SpeciesResponse> CreateAsync(CreateSpeciesRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required");
        if (req.UnitCost < 0) throw new ArgumentException("UnitCost cannot be negative");
        if (req.SellPrice is not null && req.SellPrice < 0) throw new ArgumentException("SellPrice cannot be negative");

        // ✅ NEW validation
        if (req.Vat < 0) throw new ArgumentException("Vat cannot be negative");
        if (req.QtyOnHandHub < 0) throw new ArgumentException("QtyOnHandHub cannot be negative");
        if (req.QtyBookedOutForDelivery < 0) throw new ArgumentException("QtyBookedOutForDelivery cannot be negative");
        if (req.QtyBookedOutForDelivery > req.QtyOnHandHub)
            throw new ArgumentException("QtyBookedOutForDelivery cannot exceed QtyOnHandHub");

        var species = new Species
        {
            SpeciesId = "spc_" + Guid.NewGuid().ToString("N")[..10],
            Name = name,
            UnitCost = req.UnitCost,
            SellPrice = req.SellPrice,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,

            // ✅ NEW fields set on create
            Vat = req.Vat,
            QtyOnHandHub = req.QtyOnHandHub,
            QtyBookedOutForDelivery = req.QtyBookedOutForDelivery
        };

        await _repo.CreateAsync(species, ct);
        return ToResponse(species);
    }

    public async Task<List<SpeciesResponse>> ListAsync(CancellationToken ct)
    {
        var list = await _repo.ListAsync(ct);
        return list.Select(ToResponse).ToList();
    }

    public async Task<SpeciesResponse?> GetAsync(string speciesId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(speciesId)) return null;

        var s = await _repo.GetAsync(speciesId.Trim(), ct);
        return s is null ? null : ToResponse(s);
    }

    public async Task<SpeciesResponse?> UpdateAsync(string speciesId, UpdateSpeciesRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(speciesId)) return null;

        var existing = await _repo.GetAsync(speciesId.Trim(), ct);
        if (existing is null) return null;

        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required");
        if (req.UnitCost < 0) throw new ArgumentException("UnitCost cannot be negative");
        if (req.SellPrice is not null && req.SellPrice < 0) throw new ArgumentException("SellPrice cannot be negative");

        // ✅ NEW validation
        if (req.Vat < 0) throw new ArgumentException("Vat cannot be negative");
        if (req.QtyOnHandHub < 0) throw new ArgumentException("QtyOnHandHub cannot be negative");
        if (req.QtyBookedOutForDelivery < 0) throw new ArgumentException("QtyBookedOutForDelivery cannot be negative");
        if (req.QtyBookedOutForDelivery > req.QtyOnHandHub)
            throw new ArgumentException("QtyBookedOutForDelivery cannot exceed QtyOnHandHub");

        existing.Name = name;
        existing.UnitCost = req.UnitCost;
        existing.SellPrice = req.SellPrice;
        existing.IsActive = req.IsActive;

        // ✅ NEW fields set on update (this was the bug)
        existing.Vat = req.Vat;
        existing.QtyOnHandHub = req.QtyOnHandHub;
        existing.QtyBookedOutForDelivery = req.QtyBookedOutForDelivery;

        await _repo.UpdateAsync(existing, ct);
        return ToResponse(existing);
    }

    private static SpeciesResponse ToResponse(Species s)
    {
        // If your SpeciesResponse includes QtyAvailable, set it here.
        // If it doesn't, remove the QtyAvailable assignment below.
        return new SpeciesResponse
        {
            SpeciesId = s.SpeciesId,
            Name = s.Name,
            UnitCost = s.UnitCost,
            SellPrice = s.SellPrice,
            IsActive = s.IsActive,
            CreatedAtUtc = s.CreatedAtUtc,

            Vat = s.Vat,
            QtyOnHandHub = s.QtyOnHandHub,
            QtyBookedOutForDelivery = s.QtyBookedOutForDelivery,

            //QtyAvailable = s.QtyOnHandHub - s.QtyBookedOutForDelivery
        };
    }
}