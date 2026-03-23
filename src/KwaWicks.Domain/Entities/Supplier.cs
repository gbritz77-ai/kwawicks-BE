namespace KwaWicks.Domain.Entities;

public class Supplier
{
    public string SupplierId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public SupplierAddress Address { get; set; } = new();
    public SupplierContact ContactPerson { get; set; } = new();
    public SupplierContactFinance ContactFinance { get; set; } = new();
    public SupplierBankDetails BankDetails { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SupplierAddress
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";
}

public class SupplierContact
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
}

public class SupplierContactFinance
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
}

public class SupplierBankDetails
{
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string BranchCode { get; set; } = "";
    public string AccountType { get; set; } = "";
}
