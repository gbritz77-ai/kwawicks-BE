namespace KwaWicks.Domain.Entities;

public class PettyCashEntry
{
    public string EntryId { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "Out";          // In | Out
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Other";    // Fuel | Maintenance | Supplies | DriverExpense | Other
    public string RecipientName { get; set; } = "";    // Who received the cash
    public string RecordedBy { get; set; } = "";       // Cognito username of recorder
    public string EntryDate { get; set; } = "";        // YYYY-MM-DD
    public string CashupId { get; set; } = "";         // Set when included in a cashup
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class PettyCashup
{
    public string CashupId { get; set; } = Guid.NewGuid().ToString("N");
    public string CashupDate { get; set; } = "";       // YYYY-MM-DD
    public decimal OpeningBalance { get; set; }
    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
    public decimal ExpectedBalance { get; set; }       // Opening + In - Out
    public decimal ActualBalance { get; set; }         // Physically counted
    public decimal Variance { get; set; }              // Actual - Expected
    public string Notes { get; set; } = "";
    public string ClosedBy { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
