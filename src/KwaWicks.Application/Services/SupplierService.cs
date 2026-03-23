using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repo;

    public SupplierService(ISupplierRepository repo) => _repo = repo;

    public async Task<SupplierResponse> CreateAsync(CreateSupplierRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Supplier name is required.");
        var supplier = new Supplier
        {
            Name = req.Name.Trim(),
            Address = new SupplierAddress { Street = req.Address.Street, City = req.Address.City, Province = req.Address.Province, PostalCode = req.Address.PostalCode },
            ContactPerson = new SupplierContact { Name = req.ContactPerson.Name, Phone = req.ContactPerson.Phone },
            ContactFinance = new SupplierContactFinance { Name = req.ContactFinance.Name, Phone = req.ContactFinance.Phone, Email = req.ContactFinance.Email },
            BankDetails = new SupplierBankDetails { BankName = req.BankDetails.BankName, AccountNumber = req.BankDetails.AccountNumber, BranchCode = req.BankDetails.BranchCode, AccountType = req.BankDetails.AccountType }
        };
        await _repo.CreateAsync(supplier, ct);
        return MapToResponse(supplier);
    }

    public async Task<SupplierResponse?> GetAsync(string supplierId, CancellationToken ct = default)
    {
        var s = await _repo.GetAsync(supplierId, ct);
        return s == null ? null : MapToResponse(s);
    }

    public async Task<List<SupplierResponse>> ListAsync(CancellationToken ct = default)
    {
        var suppliers = await _repo.ListAsync(ct);
        return suppliers.Select(MapToResponse).ToList();
    }

    public async Task<SupplierResponse> UpdateAsync(string supplierId, UpdateSupplierRequest req, CancellationToken ct = default)
    {
        var supplier = await _repo.GetAsync(supplierId, ct)
            ?? throw new InvalidOperationException($"Supplier not found: {supplierId}");
        supplier.Name = req.Name.Trim();
        supplier.Address = new SupplierAddress { Street = req.Address.Street, City = req.Address.City, Province = req.Address.Province, PostalCode = req.Address.PostalCode };
        supplier.ContactPerson = new SupplierContact { Name = req.ContactPerson.Name, Phone = req.ContactPerson.Phone };
        supplier.ContactFinance = new SupplierContactFinance { Name = req.ContactFinance.Name, Phone = req.ContactFinance.Phone, Email = req.ContactFinance.Email };
        supplier.BankDetails = new SupplierBankDetails { BankName = req.BankDetails.BankName, AccountNumber = req.BankDetails.AccountNumber, BranchCode = req.BankDetails.BranchCode, AccountType = req.BankDetails.AccountType };
        await _repo.UpdateAsync(supplier, ct);
        return MapToResponse(supplier);
    }

    public Task DeleteAsync(string supplierId, CancellationToken ct = default)
        => _repo.DeleteAsync(supplierId, ct);

    private static SupplierResponse MapToResponse(Supplier s) => new()
    {
        SupplierId = s.SupplierId,
        Name = s.Name,
        Address = new SupplierAddressDto { Street = s.Address.Street, City = s.Address.City, Province = s.Address.Province, PostalCode = s.Address.PostalCode },
        ContactPerson = new SupplierContactDto { Name = s.ContactPerson.Name, Phone = s.ContactPerson.Phone },
        ContactFinance = new SupplierContactFinanceDto { Name = s.ContactFinance.Name, Phone = s.ContactFinance.Phone, Email = s.ContactFinance.Email },
        BankDetails = new SupplierBankDetailsDto { BankName = s.BankDetails.BankName, AccountNumber = s.BankDetails.AccountNumber, BranchCode = s.BankDetails.BranchCode, AccountType = s.BankDetails.AccountType },
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}
