namespace KwaWicks.Application.DTOs;

public class SupplierAddressDto
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";
}

public class SupplierContactDto
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
}

public class SupplierContactFinanceDto
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
}

public class SupplierBankDetailsDto
{
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string BranchCode { get; set; } = "";
    public string AccountType { get; set; } = "";
}

public class CreateSupplierRequest
{
    public string Name { get; set; } = "";
    public SupplierAddressDto Address { get; set; } = new();
    public SupplierContactDto ContactPerson { get; set; } = new();
    public SupplierContactFinanceDto ContactFinance { get; set; } = new();
    public SupplierBankDetailsDto BankDetails { get; set; } = new();
}

public class UpdateSupplierRequest
{
    public string Name { get; set; } = "";
    public SupplierAddressDto Address { get; set; } = new();
    public SupplierContactDto ContactPerson { get; set; } = new();
    public SupplierContactFinanceDto ContactFinance { get; set; } = new();
    public SupplierBankDetailsDto BankDetails { get; set; } = new();
}

public class SupplierResponse
{
    public string SupplierId { get; set; } = "";
    public string Name { get; set; } = "";
    public SupplierAddressDto Address { get; set; } = new();
    public SupplierContactDto ContactPerson { get; set; } = new();
    public SupplierContactFinanceDto ContactFinance { get; set; } = new();
    public SupplierBankDetailsDto BankDetails { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
