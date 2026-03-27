namespace KwaWicks.Application.DTOs;

// ── Entry ──────────────────────────────────────────────────────────────────

public class CreatePettyCashEntryRequest
{
    public string Type { get; set; } = "Out";          // In | Out
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string RecipientName { get; set; } = "";
    public string EntryDate { get; set; } = "";        // YYYY-MM-DD
    public string AssignedDriverId { get; set; } = ""; // optional — Cognito username of driver
}

public class PettyCashEntryDto
{
    public string EntryId { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public string EntryDate { get; set; } = "";
    public string CashupId { get; set; } = "";
    public string AssignedDriverId { get; set; } = "";
    public string SlipS3Key { get; set; } = "";
    public string? SlipUploadUrl { get; set; }         // populated on-demand
    public DateTime CreatedAtUtc { get; set; }
}

public class ConfirmSlipUploadRequest
{
    public string S3Key { get; set; } = "";
}

// ── Cashup ─────────────────────────────────────────────────────────────────

public class CreateCashupRequest
{
    public decimal ActualBalance { get; set; }
    public string Notes { get; set; } = "";
    public string CashupDate { get; set; } = "";       // YYYY-MM-DD
}

public class PettyCashupDto
{
    public string CashupId { get; set; } = "";
    public string CashupDate { get; set; } = "";
    public decimal OpeningBalance { get; set; }
    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal ActualBalance { get; set; }
    public decimal Variance { get; set; }
    public string Notes { get; set; } = "";
    public string ClosedBy { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public List<PettyCashEntryDto> Entries { get; set; } = new();
}

// ── Summary ────────────────────────────────────────────────────────────────

public class PettyCashSummaryDto
{
    public decimal CurrentBalance { get; set; }
    public decimal TotalInSinceLastCashup { get; set; }
    public decimal TotalOutSinceLastCashup { get; set; }
    public int OpenEntryCount { get; set; }
    public string? LastCashupDate { get; set; }
    public List<PettyCashEntryDto> OpenEntries { get; set; } = new();
}
