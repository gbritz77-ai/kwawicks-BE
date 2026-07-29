using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class PettyCashService : IPettyCashService
{
    private readonly IPettyCashRepository _repo;
    private readonly IS3Service _s3;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IClientCreditRepository _creditRepo;

    public PettyCashService(
        IPettyCashRepository repo,
        IS3Service s3,
        IInvoiceRepository invoiceRepo,
        IClientCreditRepository creditRepo)
    {
        _repo = repo;
        _s3 = s3;
        _invoiceRepo = invoiceRepo;
        _creditRepo = creditRepo;
    }

    public async Task<PettyCashEntryDto> CreateEntryAsync(
        CreatePettyCashEntryRequest request, string recordedBy, CancellationToken ct)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("Description is required.");
        if (string.IsNullOrWhiteSpace(request.EntryDate))
            request.EntryDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var entry = new PettyCashEntry
        {
            Type = request.Type == "In" ? "In" : "Out",
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Other" : request.Category,
            RecipientName = request.RecipientName?.Trim() ?? "",
            RecordedBy = recordedBy,
            EntryDate = request.EntryDate,
            AssignedDriverId = request.AssignedDriverId?.Trim() ?? ""
        };

        await _repo.CreateEntryAsync(entry, ct);
        return MapEntry(entry);
    }

    public async Task<List<PettyCashEntryDto>> ListEntriesAsync(string? from, string? to, CancellationToken ct)
    {
        var entries = await _repo.ListEntriesAsync(from, to, ct);
        return entries.Select(MapEntry).ToList();
    }

    public async Task<PettyCashSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var openEntries = await _repo.ListOpenEntriesAsync(ct);
        var lastCashup = await _repo.GetLatestCashupAsync(ct);

        var totalIn = openEntries.Where(e => e.Type == "In").Sum(e => e.Amount);
        var totalOut = openEntries.Where(e => e.Type == "Out").Sum(e => e.Amount);
        var openingBalance = lastCashup?.ActualBalance ?? 0m;
        var currentBalance = openingBalance + totalIn - totalOut;

        // Use the exact cashup timestamp so the new period only includes
        // sales/deposits that occurred AFTER the cashup was confirmed.
        DateTime? since = lastCashup?.CreatedAtUtc;
        var cashFromHubSales = await _invoiceRepo.SumCashSalesAsync(since, ct);
        var cashFromCreditDeposits = await _creditRepo.SumCashDepositsAsync(since, ct);

        var (hubOverride, depositOverride) = await _repo.GetCashOverridesAsync(ct);
        var effectiveHub = hubOverride ?? cashFromHubSales;
        var effectiveDeposits = depositOverride ?? cashFromCreditDeposits;

        return new PettyCashSummaryDto
        {
            CurrentBalance = currentBalance,
            TotalInSinceLastCashup = totalIn,
            TotalOutSinceLastCashup = totalOut,
            OpenEntryCount = openEntries.Count,
            LastCashupDate = lastCashup?.CashupDate,
            OpenEntries = openEntries.Select(MapEntry).ToList(),
            CashFromHubSales = effectiveHub,
            CashFromCreditDeposits = effectiveDeposits,
            TotalCashInCustody = currentBalance + effectiveHub + effectiveDeposits,
            HubSalesCashOverride = hubOverride,
            ClientDepositsCashOverride = depositOverride
        };
    }

    public async Task<PettyCashupDto> CreateCashupAsync(
        CreateCashupRequest request, string closedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CashupDate))
            request.CashupDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var openEntries = await _repo.ListOpenEntriesAsync(ct);
        var lastCashup = await _repo.GetLatestCashupAsync(ct);

        var openingBalance = lastCashup?.ActualBalance ?? 0m;
        var totalIn = openEntries.Where(e => e.Type == "In").Sum(e => e.Amount);
        var totalOut = openEntries.Where(e => e.Type == "Out").Sum(e => e.Amount);
        var expectedBalance = openingBalance + totalIn - totalOut;

        var cashup = new PettyCashup
        {
            CashupDate = request.CashupDate,
            OpeningBalance = openingBalance,
            TotalIn = totalIn,
            TotalOut = totalOut,
            ExpectedBalance = expectedBalance,
            ActualBalance = request.ActualBalance,
            Variance = request.ActualBalance - expectedBalance,
            Notes = request.Notes?.Trim() ?? "",
            ClosedBy = closedBy
        };

        await _repo.CreateCashupAsync(cashup, ct);
        await _repo.MarkEntriesCashedUpAsync(openEntries.Select(e => e.EntryId), cashup.CashupId, ct);
        // Clear manual overrides so the new period calculates fresh from real data
        await _repo.SetCashOverridesAsync(null, null, ct);

        return new PettyCashupDto
        {
            CashupId = cashup.CashupId,
            CashupDate = cashup.CashupDate,
            OpeningBalance = cashup.OpeningBalance,
            TotalIn = cashup.TotalIn,
            TotalOut = cashup.TotalOut,
            ExpectedBalance = cashup.ExpectedBalance,
            ActualBalance = cashup.ActualBalance,
            Variance = cashup.Variance,
            Notes = cashup.Notes,
            ClosedBy = cashup.ClosedBy,
            CreatedAtUtc = cashup.CreatedAtUtc,
            Entries = openEntries.Select(MapEntry).ToList()
        };
    }

    public async Task<List<PettyCashupDto>> ListCashupsAsync(CancellationToken ct)
    {
        var cashups = await _repo.ListCashupsAsync(ct);
        return cashups.Select(c => new PettyCashupDto
        {
            CashupId = c.CashupId,
            CashupDate = c.CashupDate,
            OpeningBalance = c.OpeningBalance,
            TotalIn = c.TotalIn,
            TotalOut = c.TotalOut,
            ExpectedBalance = c.ExpectedBalance,
            ActualBalance = c.ActualBalance,
            Variance = c.Variance,
            Notes = c.Notes,
            ClosedBy = c.ClosedBy,
            CreatedAtUtc = c.CreatedAtUtc
        }).ToList();
    }

    public async Task<List<PettyCashEntryDto>> ListDriverEntriesAsync(string driverId, CancellationToken ct)
    {
        var entries = await _repo.ListDriverEntriesAsync(driverId, ct);
        return entries.Select(MapEntry).ToList();
    }

    public async Task<string> GetSlipUploadUrlAsync(string entryId, CancellationToken ct)
    {
        var s3Key = $"petty-cash-slips/{entryId}/{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";
        return await _s3.GeneratePresignedUploadUrlAsync(s3Key, "image/jpeg", ct);
    }

    public async Task<PettyCashEntryDto> ConfirmSlipUploadedAsync(string entryId, string s3Key, CancellationToken ct)
    {
        await _repo.UpdateEntrySlipAsync(entryId, s3Key, ct);
        var entry = await _repo.GetEntryAsync(entryId, ct)
                    ?? throw new ArgumentException("Entry not found.");
        return MapEntry(entry);
    }

    public Task ClearAllAsync(CancellationToken ct) => _repo.ClearAllAsync(ct);

    public Task SetCashOverridesAsync(decimal? hubSales, decimal? clientDeposits, CancellationToken ct)
        => _repo.SetCashOverridesAsync(hubSales, clientDeposits, ct);

    public async Task SetFloatAsync(decimal floatAmount, CancellationToken ct)
    {
        var latest = await _repo.GetLatestCashupAsync(ct);
        if (latest != null)
        {
            await _repo.UpdateCashupActualBalanceAsync(latest.CashupId, floatAmount, ct);
        }
        else
        {
            // No cashup exists yet — create one, but use MinValue so the hub-sales/deposits
            // "since" filter includes ALL historical records (same behaviour as no cashup at all).
            var cashup = new PettyCashup
            {
                CashupDate = "1970-01-01",
                OpeningBalance = 0,
                TotalIn = 0,
                TotalOut = 0,
                ExpectedBalance = 0,
                ActualBalance = floatAmount,
                Variance = 0,
                Notes = "Float initialised",
                ClosedBy = "system",
                CreatedAtUtc = DateTime.MinValue
            };
            await _repo.CreateCashupAsync(cashup, ct);
        }
    }

    private static PettyCashEntryDto MapEntry(PettyCashEntry e) => new()
    {
        EntryId = e.EntryId,
        Type = e.Type,
        Amount = e.Amount,
        Description = e.Description,
        Category = e.Category,
        RecipientName = e.RecipientName,
        RecordedBy = e.RecordedBy,
        EntryDate = e.EntryDate,
        CashupId = e.CashupId,
        AssignedDriverId = e.AssignedDriverId,
        SlipS3Key = e.SlipS3Key,
        CreatedAtUtc = e.CreatedAtUtc
    };
}
