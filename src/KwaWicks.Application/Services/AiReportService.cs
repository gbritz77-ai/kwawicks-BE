using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Application.Services;

public class AiReportService : IAiReportService
{
    private readonly HttpClient _http;
    private readonly IInvoiceRepository _invoices;
    private readonly IClientRepository _clients;
    private readonly IClientCreditRepository _credits;
    private readonly ICollectionRequestRepository _collections;
    private readonly IStaffMemberRepository _staff;
    private readonly IPettyCashService _pettyCash;
    private readonly ISpeciesRepository _species;
    private readonly IDeliveryOrderRepository _deliveryOrders;
    private readonly IDeliveryRunRepository _deliveryRuns;
    private readonly IFuelRepository _fuel;
    private readonly ISlaughterRepository _slaughter;
    private readonly IStockLossRepository _stockLosses;
    private readonly IVehicleRepository _vehicles;
    private readonly IProcurementOrderRepository _procurementOrders;
    private readonly IDriverStockAllocationRepository _driverAllocations;
    private readonly IDipTankRepository _dipTanks;
    private readonly ISupplierRepository _suppliers;

    public AiReportService(
        HttpClient http,
        IInvoiceRepository invoices,
        IClientRepository clients,
        IClientCreditRepository credits,
        ICollectionRequestRepository collections,
        IStaffMemberRepository staff,
        IPettyCashService pettyCash,
        ISpeciesRepository species,
        IDeliveryOrderRepository deliveryOrders,
        IDeliveryRunRepository deliveryRuns,
        IFuelRepository fuel,
        ISlaughterRepository slaughter,
        IStockLossRepository stockLosses,
        IVehicleRepository vehicles,
        IProcurementOrderRepository procurementOrders,
        IDriverStockAllocationRepository driverAllocations,
        IDipTankRepository dipTanks,
        ISupplierRepository suppliers)
    {
        _http = http;
        _invoices = invoices;
        _clients = clients;
        _credits = credits;
        _collections = collections;
        _staff = staff;
        _pettyCash = pettyCash;
        _species = species;
        _deliveryOrders = deliveryOrders;
        _deliveryRuns = deliveryRuns;
        _fuel = fuel;
        _slaughter = slaughter;
        _stockLosses = stockLosses;
        _vehicles = vehicles;
        _procurementOrders = procurementOrders;
        _driverAllocations = driverAllocations;
        _dipTanks = dipTanks;
        _suppliers = suppliers;
    }

    public async Task<AiReportResult> RunReportAsync(string prompt, CancellationToken ct)
    {
        var toolsJson    = BuildTools().ToJsonString();
        var messagesJson = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["content"] = prompt }
        }.ToJsonString();

        var today = DateTime.UtcNow.AddHours(2); // SAST = UTC+2
        var systemPrompt =
            $"Today's date is {today:yyyy-MM-dd} (South Africa Standard Time). " +
            "You are a business intelligence assistant for KwaWicks, a poultry distribution company in South Africa. " +
            "When the user refers to 'this month', 'today', 'this week', etc., use today's date to calculate the correct date range. " +
            "Use the available tools to fetch live data, then respond with ONLY a raw JSON object — no markdown fences, no preamble, no explanation outside the JSON. " +
            "The JSON must have exactly these three keys:\n" +
            "{ \"narrative\": \"<one paragraph summary>\", \"columns\": [\"Col1\", \"Col2\"], \"rows\": [[\"v1\",\"v2\"], ...] }\n" +
            "Rules for the narrative: write plain English only — absolutely no JSON, no brackets, no raw data values. " +
            "Rules for rows: MAXIMUM 100 rows in the response. ALWAYS aggregate and group data — never return one row per transaction or per invoice line. " +
            "For example: group by customer+species+payment to show totals; group by date to show daily summaries; group by species to show per-product totals. " +
            "If the raw data has more records than can fit in 100 grouped rows, summarise further (e.g. top 100 by value) and mention the total count in the narrative. " +
            "Amounts: South African Rand format e.g. 'R 1 234.56'. Dates: YYYY-MM-DD. " +
            "If no tabular data applies leave columns and rows as empty arrays. " +
            "CRITICAL: Your entire response must be valid JSON starting with { and ending with }. Do NOT wrap in markdown code fences.";

        // Agentic loop — max 8 rounds to allow multi-tool queries
        for (var round = 0; round < 8; round++)
        {
            var requestBody = new JsonObject
            {
                ["model"]      = "claude-haiku-4-5-20251001",
                ["max_tokens"] = 8192,
                ["system"]     = systemPrompt,
                ["tools"]      = JsonNode.Parse(toolsJson),
                ["messages"]   = JsonNode.Parse(messagesJson)
            };

            var response = await _http.PostAsync(
                "https://api.anthropic.com/v1/messages",
                new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json"),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Anthropic API error {(int)response.StatusCode}: {errorBody}");
            }

            var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct)
                       ?? throw new InvalidOperationException("Empty response from Anthropic API");

            var stopReason  = body["stop_reason"]?.GetValue<string>();
            var contentJson = body["content"]?.ToJsonString() ?? "[]";
            var content     = JsonNode.Parse(contentJson)!.AsArray();

            var toolUseBlocks = content
                .OfType<JsonObject>()
                .Where(b => b["type"]?.GetValue<string>() == "tool_use")
                .ToList();

            var assistantTurn = new JsonObject
            {
                ["role"]    = "assistant",
                ["content"] = JsonNode.Parse(contentJson)
            };
            var msgArray = JsonNode.Parse(messagesJson)!.AsArray();
            msgArray.Add(assistantTurn);

            if (stopReason == "end_turn" || toolUseBlocks.Count == 0)
            {
                var text = content
                    .OfType<JsonObject>()
                    .Where(b => b["type"]?.GetValue<string>() == "text")
                    .Select(b => b["text"]?.GetValue<string>() ?? "")
                    .FirstOrDefault() ?? "";

                return ParseFinalResponse(text);
            }

            var toolResults = new JsonArray();
            foreach (var block in toolUseBlocks)
            {
                var toolName  = block["name"]?.GetValue<string>() ?? "";
                var toolUseId = block["id"]?.GetValue<string>()   ?? "";
                var input     = block["input"]?.AsObject()         ?? new JsonObject();

                var result = await ExecuteToolAsync(toolName, input, ct);

                toolResults.Add(new JsonObject
                {
                    ["type"]        = "tool_result",
                    ["tool_use_id"] = toolUseId,
                    ["content"]     = result
                });
            }

            msgArray.Add(new JsonObject
            {
                ["role"]    = "user",
                ["content"] = JsonNode.Parse(toolResults.ToJsonString())
            });

            messagesJson = msgArray.ToJsonString();
        }

        return new AiReportResult { Narrative = "Unable to generate report — maximum tool call rounds reached." };
    }

    // ── Tool execution ──────────────────────────────────────────────────────

    private async Task<string> ExecuteToolAsync(string toolName, JsonObject input, CancellationToken ct)
    {
        try
        {
            return toolName switch
            {
                "list_clients"                => await ListClientsAsync(ct),
                "get_outstanding_balances"    => await GetOutstandingBalancesAsync(ct),
                "get_sales_summary"           => await GetSalesSummaryAsync(input, ct),
                "list_invoices"               => await ListInvoicesAsync(input, ct),
                "get_invoice_line_items"      => await GetInvoiceLineItemsAsync(input, ct),
                "get_petty_cash_summary"      => await GetPettyCashSummaryAsync(ct),
                "list_collections"            => await ListCollectionsAsync(input, ct),
                "list_staff_members"          => await ListStaffMembersAsync(ct),
                "list_species"                => await ListSpeciesAsync(ct),
                "list_delivery_orders"        => await ListDeliveryOrdersAsync(input, ct),
                "list_fuel_records"           => await ListFuelRecordsAsync(input, ct),
                "list_slaughter_batches"      => await ListSlaughterBatchesAsync(input, ct),
                "list_stock_losses"           => await ListStockLossesAsync(input, ct),
                "list_vehicles"               => await ListVehiclesAsync(ct),
                "list_procurement_orders"     => await ListProcurementOrdersAsync(input, ct),
                "list_driver_sales"           => await ListDriverSalesAsync(input, ct),
                "list_dip_tank_readings"      => await ListDipTankReadingsAsync(ct),
                "list_suppliers"              => await ListSuppliersAsync(ct),
                _                             => $"Unknown tool: {toolName}"
            };
        }
        catch (Exception ex)
        {
            return $"Error executing {toolName}: {ex.Message}";
        }
    }

    // ── Tool implementations ────────────────────────────────────────────────

    private async Task<string> ListClientsAsync(CancellationToken ct)
    {
        var clients = await _clients.ListAsync(200, ct);
        return JsonSerializer.Serialize(clients.Select(c => new
        {
            c.ClientId, c.ClientName, c.ClientPhone, c.ClientType, c.IsWalkIn
        }));
    }

    private async Task<string> GetOutstandingBalancesAsync(CancellationToken ct)
    {
        var clients     = await _clients.ListAsync(200, ct);
        var allInvoices = await _invoices.ListAsync(null, null, ct);

        var result = clients.Select(c =>
        {
            var inv = allInvoices.Where(i => i.CustomerId == c.ClientId && i.Status != "Cancelled").ToList();
            return new
            {
                ClientName   = c.ClientName,
                TotalBilled  = inv.Sum(i => i.GrandTotal),
                TotalPaid    = inv.Sum(i => i.AmountPaid),
                Outstanding  = inv.Sum(i => i.GrandTotal - i.AmountPaid),
                InvoiceCount = inv.Count
            };
        })
        .Where(r => r.Outstanding != 0)
        .OrderByDescending(r => r.Outstanding)
        .ToList();

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetSalesSummaryAsync(JsonObject input, CancellationToken ct)
    {
        var from = input["from_date"]?.GetValue<string>();
        var to   = input["to_date"]?.GetValue<string>();

        var all = await _invoices.ListAsync(null, null, ct);
        var filtered = all.Where(i => i.Status != "Cancelled");
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) filtered = filtered.Where(i => i.CreatedAt >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) filtered = filtered.Where(i => i.CreatedAt <= t.AddDays(1));

        var list = filtered.ToList();
        return JsonSerializer.Serialize(new
        {
            TotalInvoices    = list.Count,
            TotalRevenue     = list.Sum(i => i.GrandTotal),
            TotalCash        = list.Where(i => i.PaymentType == "Cash").Sum(i => i.GrandTotal),
            TotalEft         = list.Where(i => i.PaymentType == "EFT").Sum(i => i.GrandTotal),
            TotalCredit      = list.Where(i => i.PaymentType == "Credit").Sum(i => i.GrandTotal),
            TotalPaid        = list.Sum(i => i.AmountPaid),
            TotalOutstanding = list.Sum(i => i.GrandTotal - i.AmountPaid),
            ByDate = list
                .GroupBy(i => i.CreatedAt.ToString("yyyy-MM-dd"))
                .Select(g => new { Date = g.Key, Revenue = g.Sum(i => i.GrandTotal), Count = g.Count() })
                .OrderByDescending(g => g.Date).ToList()
        });
    }

    private async Task<string> ListInvoicesAsync(JsonObject input, CancellationToken ct)
    {
        var status      = input["status"]?.GetValue<string>();
        var paymentType = input["payment_type"]?.GetValue<string>();
        var from        = input["from_date"]?.GetValue<string>();
        var to          = input["to_date"]?.GetValue<string>();

        var all       = await _invoices.ListAsync(null, null, ct);
        var clientMap = (await _clients.ListAsync(200, ct)).ToDictionary(c => c.ClientId, c => c.ClientName);
        var q         = all.AsEnumerable();
        if (!string.IsNullOrEmpty(status))      q = q.Where(i => string.Equals(i.Status,      status,      StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(paymentType)) q = q.Where(i => string.Equals(i.PaymentType, paymentType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) q = q.Where(i => i.CreatedAt >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) q = q.Where(i => i.CreatedAt <= t.AddDays(1));

        var list       = q.OrderByDescending(i => i.CreatedAt).ToList();
        var returned   = list.Take(200).ToList();
        return JsonSerializer.Serialize(new
        {
            TotalCount   = list.Count,
            ReturnedCount = returned.Count,
            Note         = list.Count > 200 ? $"Only the 200 most recent of {list.Count} invoices are included. Use from_date/to_date to narrow the range for complete accuracy." : null,
            Invoices     = returned.Select(i => new
            {
                i.InvoiceNumber,
                CustomerName = clientMap.TryGetValue(i.CustomerId, out var cn) ? cn : i.CustomerId,
                CreatedAt    = i.CreatedAt.ToString("yyyy-MM-dd"),
                i.Status, i.PaymentType, i.PaymentStatus, i.GrandTotal, i.AmountPaid,
                Outstanding  = i.GrandTotal - i.AmountPaid
            })
        });
    }

    private async Task<string> GetInvoiceLineItemsAsync(JsonObject input, CancellationToken ct)
    {
        var from       = input["from_date"]?.GetValue<string>();
        var to         = input["to_date"]?.GetValue<string>();
        var customerId = input["customer_id"]?.GetValue<string>();

        var allInvoices = await _invoices.ListAsync(null, null, ct);
        var speciesMap  = (await _species.ListAsync(ct)).ToDictionary(s => s.SpeciesId, s => s.Name);
        var clientMap   = (await _clients.ListAsync(200, ct)).ToDictionary(c => c.ClientId, c => c.ClientName);

        // Filter invoices first (date/customer) — do NOT cap invoices here; cap the final line-item rows
        var q = allInvoices.Where(i => i.Status != "Cancelled");
        if (!string.IsNullOrEmpty(customerId)) q = q.Where(i => i.CustomerId == customerId);
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) q = q.Where(i => i.CreatedAt >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) q = q.Where(i => i.CreatedAt <= t.AddDays(1));

        var matchedInvoices = q.OrderByDescending(i => i.CreatedAt).ToList();

        // Expand all line items across all matched invoices, then aggregate
        var lines = matchedInvoices
            .SelectMany(i => i.Lines.Select(l => new
            {
                CustomerName = clientMap.TryGetValue(i.CustomerId, out var cn) ? cn : i.CustomerId,
                i.PaymentType,
                SpeciesName  = speciesMap.TryGetValue(l.SpeciesId, out var sn) ? sn : l.SpeciesId,
                l.Quantity,
                l.UnitPrice,
                l.LineTotal
            }))
            .GroupBy(l => new { l.CustomerName, l.SpeciesName, l.PaymentType, l.UnitPrice })
            .Select(g => new
            {
                g.Key.CustomerName,
                g.Key.SpeciesName,
                g.Key.PaymentType,
                g.Key.UnitPrice,
                TotalQty       = g.Sum(x => x.Quantity),
                TotalLineTotal = g.Sum(x => x.LineTotal)
            })
            .OrderBy(r => r.CustomerName).ThenBy(r => r.SpeciesName)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            InvoiceCount = matchedInvoices.Count,
            AggregatedRows = lines.Count,
            Note = $"All {matchedInvoices.Count} invoices in the date range are included. Data is pre-aggregated by customer+species+payment+price.",
            Lines = lines
        });
    }

    private async Task<string> GetPettyCashSummaryAsync(CancellationToken ct)
    {
        var s = await _pettyCash.GetSummaryAsync(ct);
        return JsonSerializer.Serialize(new
        {
            s.CurrentBalance, s.TotalInSinceLastCashup, s.TotalOutSinceLastCashup,
            s.OpenEntryCount, s.LastCashupDate, s.CashFromHubSales,
            s.CashFromCreditDeposits, s.TotalCashInCustody,
            OpenEntries = s.OpenEntries.Select(e => new
            {
                e.EntryDate, e.Type, e.Category, e.Description, e.Amount, e.RecipientName
            })
        });
    }

    private async Task<string> ListCollectionsAsync(JsonObject input, CancellationToken ct)
    {
        var status = input["status"]?.GetValue<string>();
        var from   = input["from_date"]?.GetValue<string>();
        var to     = input["to_date"]?.GetValue<string>();

        var all = await _collections.ListAsync(status: status, ct: ct);
        var q   = all.AsEnumerable();
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) q = q.Where(c => (c.CollectionDate ?? c.CreatedAt) >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) q = q.Where(c => (c.CollectionDate ?? c.CreatedAt) <= t.AddDays(1));

        var list     = q.OrderByDescending(c => c.CollectionDate ?? c.CreatedAt).ToList();
        var returned = list.Take(200).ToList();
        return JsonSerializer.Serialize(new
        {
            TotalCount    = list.Count,
            ReturnedCount = returned.Count,
            Note          = list.Count > 200 ? $"Only 200 of {list.Count} collections returned. Use from_date/to_date to narrow the range." : null,
            Collections   = returned.Select(c => new
            {
                c.CollectionRequestId, c.SupplierName, c.AssignedDriverName, c.Status,
                Date          = c.CollectionDate?.ToString("yyyy-MM-dd") ?? c.CreatedAt.ToString("yyyy-MM-dd"),
                TotalOrdered  = c.Lines.Sum(l => l.OrderedQty),
                TotalLoaded   = c.Lines.Sum(l => l.LoadedQty),
                TotalReceived = c.Lines.Sum(l => l.ReceivedQty),
                TotalDead     = c.Lines.Sum(l => l.DeadQty),
                TotalShort    = c.Lines.Sum(l => l.ShortQty),
                TotalOver     = c.Lines.Sum(l => l.OverQty),
                Lines         = c.Lines.Select(l => new
                {
                    l.SpeciesName, l.OrderedQty, l.LoadedQty, l.ReceivedQty,
                    l.DeadQty, l.ShortQty, l.OverQty, l.DiscrepancyNotes
                })
            })
        });
    }

    private async Task<string> ListStaffMembersAsync(CancellationToken ct)
    {
        var staff = await _staff.ListAsync(ct);
        return JsonSerializer.Serialize(staff.Select(s => new
        {
            s.StaffMemberId, s.Name, s.Department, s.Phone, s.IsActive
        }));
    }

    private async Task<string> ListSpeciesAsync(CancellationToken ct)
    {
        var all = await _species.ListAsync(ct);
        return JsonSerializer.Serialize(all.Select(s => new
        {
            s.SpeciesId, s.Name, s.UnitCost, s.SellPrice, s.IsActive,
            s.QtyOnHandHub, s.QtyBookedOutForDelivery, s.Vat
        }));
    }

    private async Task<string> ListDeliveryOrdersAsync(JsonObject input, CancellationToken ct)
    {
        var status = input["status"]?.GetValue<string>();
        var from   = input["from_date"]?.GetValue<string>();
        var to     = input["to_date"]?.GetValue<string>();

        var all      = await _deliveryOrders.ListAsync(null, null, status, ct);
        var specMap  = (await _species.ListAsync(ct)).ToDictionary(s => s.SpeciesId, s => s.Name);
        var clientMap = (await _clients.ListAsync(limit: 2000, ct: ct)).ToDictionary(c => c.ClientId, c => c.ClientName);

        var q = all.AsEnumerable();
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) q = q.Where(o => o.CreatedAt >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) q = q.Where(o => o.CreatedAt <= t.AddDays(1));

        var list     = q.OrderByDescending(o => o.CreatedAt).ToList();
        var returned = list.Take(200).ToList();
        return JsonSerializer.Serialize(new
        {
            TotalCount    = list.Count,
            ReturnedCount = returned.Count,
            Note          = list.Count > 200 ? $"Only 200 of {list.Count} orders returned. Use from_date/to_date to narrow the range." : null,
            Orders = returned.Select(o => new
            {
                o.DeliveryOrderId, o.Status, o.AssignedDriverName,
                ClientName = clientMap.TryGetValue(o.CustomerId, out var cn) ? cn : o.CustomerId,
                Date       = o.CreatedAt.ToString("yyyy-MM-dd"),
                TotalLines = o.Lines.Count,
                TotalQty   = o.Lines.Sum(l => l.Quantity),
                Lines      = o.Lines.Select(l => new
                {
                    SpeciesName  = specMap.TryGetValue(l.SpeciesId, out var sn) ? sn : l.SpeciesId,
                    l.Quantity, l.UnitPrice, l.DeliveredQty,
                    l.TotalReturnedQty, l.InspectedDeadQty, l.InspectedMutilatedQty
                })
            })
        });
    }

    private async Task<string> ListFuelRecordsAsync(JsonObject input, CancellationToken ct)
    {
        var from = input["from_date"]?.GetValue<string>();
        var to   = input["to_date"]?.GetValue<string>();

        var all      = await _fuel.ListAsync(ct);
        var vehicles = (await _vehicles.ListAsync(ct)).ToDictionary(v => v.VehicleId);

        var q = all.AsEnumerable();
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) q = q.Where(r => r.IssuedAt >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) q = q.Where(r => r.IssuedAt <= t.AddDays(1));

        return JsonSerializer.Serialize(q.OrderByDescending(r => r.IssuedAt).Take(200).Select(r => new
        {
            r.IssueId,
            Date         = r.IssuedAt.ToString("yyyy-MM-dd"),
            FleetNumber  = vehicles.TryGetValue(r.VehicleId, out var v) ? v.FleetNumber : r.VehicleId,
            Registration = vehicles.TryGetValue(r.VehicleId, out var v2) ? v2.Registration : "",
            r.FuelSource, r.TankIssuedBy, r.SupplierStation,
            r.Litres, r.OdometerKm, r.CostPerLitre, r.TotalCost, r.Reference
        }));
    }

    private async Task<string> ListSlaughterBatchesAsync(JsonObject input, CancellationToken ct)
    {
        var from = input["from_date"]?.GetValue<string>();
        var to   = input["to_date"]?.GetValue<string>();

        var all = await _slaughter.ListAsync(ct);
        var q   = all.AsEnumerable();
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) q = q.Where(b => b.SlaughteredAtUtc >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) q = q.Where(b => b.SlaughteredAtUtc <= t.AddDays(1));

        var list     = q.OrderByDescending(b => b.SlaughteredAtUtc).ToList();
        var returned = list.Take(200).ToList();
        return JsonSerializer.Serialize(new
        {
            TotalCount    = list.Count,
            ReturnedCount = returned.Count,
            Note          = list.Count > 200 ? $"Only 200 of {list.Count} batches returned. Use from_date/to_date to narrow the range." : null,
            Batches = returned.Select(b => new
            {
                b.BatchId,
                Date           = b.SlaughteredAtUtc.ToString("yyyy-MM-dd"),
                b.SourceSpeciesName, b.SourceQty, b.SourceUnitCost, b.TotalInputCost,
                b.Notes,
                Yields = b.Yields.Select(y => new { y.SpeciesName, y.Qty, y.UnitCost, y.UnitPrice })
            })
        });
    }

    private async Task<string> ListStockLossesAsync(JsonObject input, CancellationToken ct)
    {
        var from = input["from_date"]?.GetValue<string>();
        var to   = input["to_date"]?.GetValue<string>();

        DateTime? fromDt = !string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f) ? f : null;
        DateTime? toDt   = !string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t) ? t.AddDays(1) : null;

        var all      = await _stockLosses.ListAsync(from: fromDt, to: toDt, ct: ct);
        var staffMap = (await _staff.ListAsync(ct)).ToDictionary(s => s.StaffMemberId, s => s.Name);
        var list     = all.OrderByDescending(l => l.CreatedAt).ToList();
        var returned = list.Take(200).ToList();
        return JsonSerializer.Serialize(new
        {
            TotalCount    = list.Count,
            ReturnedCount = returned.Count,
            Note          = list.Count > 200 ? $"Only 200 of {list.Count} records returned. Narrow with from_date/to_date." : null,
            Losses = returned.Select(l => new
            {
                l.LossId,
                Date           = l.CreatedAt.ToString("yyyy-MM-dd"),
                l.SpeciesName, l.Qty, l.AdjustmentType, l.Notes,
                RecordedBy = staffMap.TryGetValue(l.RecordedByUserId, out var sn) ? sn : l.RecordedByUserId
            })
        });
    }

    private async Task<string> ListVehiclesAsync(CancellationToken ct)
    {
        var all = await _vehicles.ListAsync(ct);
        return JsonSerializer.Serialize(all.Select(v => new
        {
            v.VehicleId, v.FleetNumber, v.Registration, v.Make, v.Model, v.Year,
            v.FuelType, v.OdoType, v.OdometerKm, v.ExpectedConsumption,
            v.LicenceExpiry, v.LastServiceOdo, v.ServiceInterval, v.IsActive, v.Notes
        }));
    }

    private async Task<string> ListProcurementOrdersAsync(JsonObject input, CancellationToken ct)
    {
        var status = input["status"]?.GetValue<string>();
        var all    = await _procurementOrders.ListAsync(status: status, ct: ct);
        return JsonSerializer.Serialize(all.OrderByDescending(o => o.CreatedAt).Take(100).Select(o => new
        {
            o.ProcurementOrderId, o.SupplierName, o.Status,
            Date       = o.CreatedAt.ToString("yyyy-MM-dd"),
            TotalQty   = o.Lines.Sum(l => l.OrderedQty),
            TotalCost  = o.Lines.Sum(l => l.OrderedQty * l.UnitCost),
            Lines      = o.Lines.Select(l => new { l.SpeciesName, l.OrderedQty, l.UnitCost })
        }));
    }

    private async Task<string> ListDriverSalesAsync(JsonObject input, CancellationToken ct)
    {
        var from     = input["from_date"]?.GetValue<string>();
        var to       = input["to_date"]?.GetValue<string>();
        var driverId = input["driver_id"]?.GetValue<string>();

        var all = await _driverAllocations.ListAsync(driverId: driverId, status: null, ct: ct);

        var sales = all
            .SelectMany(a => a.Sales.Select(s => new
            {
                a.DriverName,
                Date         = s.SoldAt.ToString("yyyy-MM-dd"),
                s.SpeciesName, s.Qty, s.UnitPrice, s.TotalAmount, s.PaymentType, s.CustomerName
            }))
            .AsEnumerable();

        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var f)) sales = sales.Where(s => DateTime.Parse(s.Date) >= f);
        if (!string.IsNullOrEmpty(to)   && DateTime.TryParse(to,   out var t)) sales = sales.Where(s => DateTime.Parse(s.Date) <= t.AddDays(1));

        return JsonSerializer.Serialize(sales.OrderByDescending(s => s.Date).Take(300).ToList());
    }

    private async Task<string> ListDipTankReadingsAsync(CancellationToken ct)
    {
        var tanks    = await _dipTanks.ListTanksAsync(ct);
        var readings = await _dipTanks.ListReadingsAsync(ct);
        var tankMap  = tanks.ToDictionary(t => t.TankId);

        return JsonSerializer.Serialize(new
        {
            Tanks = tanks.Select(t => new
            {
                t.TankId, t.Name, t.FuelType, t.CapacityLitres, t.LowQtyLitres, t.IsActive
            }),
            Readings = readings.OrderByDescending(r => r.RecordedAt).Take(100).Select(r => new
            {
                r.ReadingId,
                TankName     = tankMap.TryGetValue(r.TankId, out var tk) ? tk.Name : r.TankId,
                Date         = r.RecordedAt.ToString("yyyy-MM-dd"),
                r.ReadingLitres, r.ReadingMm, r.PctFull, r.Notes, r.RecordedBy
            })
        });
    }

    private async Task<string> ListSuppliersAsync(CancellationToken ct)
    {
        var all = await _suppliers.ListAsync(ct);
        return JsonSerializer.Serialize(all.Select(s => new
        {
            s.SupplierId, s.Name,
            City         = s.Address.City,
            Province     = s.Address.Province,
            ContactName  = s.ContactPerson.Name,
            ContactPhone = s.ContactPerson.Phone,
            FinanceEmail = s.ContactFinance.Email
        }));
    }

    // ── Tool definitions ────────────────────────────────────────────────────

    private static JsonArray BuildTools() => new()
    {
        BuildTool("list_clients",
            "Returns all clients (customers) with name, phone, type, and walk-in flag.",
            new JsonObject()),

        BuildTool("get_outstanding_balances",
            "Returns all clients with total billed, total paid, and outstanding balance. Use for debtors / accounts receivable reports.",
            new JsonObject()),

        BuildTool("get_sales_summary",
            "Returns aggregated sales: total revenue, breakdown by payment type (Cash/EFT/Credit), and daily totals. Optionally filter by date range.",
            DateRangeProps()),

        BuildTool("list_invoices",
            "Lists invoice headers (no line items). Use get_invoice_line_items when species detail is needed. Returns up to 100 invoices.",
            new JsonObject
            {
                ["status"]       = Prop("Filter by status: Draft, Confirmed, Cancelled, Paid"),
                ["payment_type"] = Prop("Filter by payment type: Cash, EFT, Credit"),
                ["from_date"]    = Prop("Start date YYYY-MM-DD"),
                ["to_date"]      = Prop("End date YYYY-MM-DD")
            }),

        BuildTool("get_invoice_line_items",
            "Returns invoice line items with species name, quantity, unit price, line total, customer name, and payment type. " +
            "Use this for any question about which species were purchased, quantities per species, or per-customer species breakdowns. Returns up to 500 lines.",
            new JsonObject
            {
                ["from_date"]    = Prop("Start date YYYY-MM-DD"),
                ["to_date"]      = Prop("End date YYYY-MM-DD"),
                ["customer_id"]  = Prop("Filter to a specific customer by ClientId")
            }),

        BuildTool("get_petty_cash_summary",
            "Returns current petty cash state: cash in safe, hub sales cash, client deposits, open entries, and last cashup date.",
            new JsonObject()),

        BuildTool("list_collections",
            "Returns collection requests (live-bird collections from suppliers): supplier, driver, status, ordered/loaded/received/dead/short/over quantities per species. Short and over use actual receipt-time values.",
            new JsonObject
            {
                ["status"]    = Prop("Filter by status: Pending, Loading, InTransit, ArrivedAtHub, HubConfirmed, FinanceAcknowledged"),
                ["from_date"] = Prop("Start date YYYY-MM-DD (filters by collection/created date)"),
                ["to_date"]   = Prop("End date YYYY-MM-DD (filters by collection/created date)")
            }),

        BuildTool("list_staff_members",
            "Returns all staff members with department, phone, and active status.",
            new JsonObject()),

        BuildTool("list_species",
            "Returns all species (product types) with unit cost, sell price, current stock on hand at hub, qty booked out for delivery, and VAT rate.",
            new JsonObject()),

        BuildTool("list_delivery_orders",
            "Returns delivery orders with status, assigned driver, client name, species names, quantities delivered, and returns/dead counts.",
            new JsonObject
            {
                ["status"]    = Prop("Filter by status: Open, OutForDelivery, Delivered"),
                ["from_date"] = Prop("Start date YYYY-MM-DD"),
                ["to_date"]   = Prop("End date YYYY-MM-DD")
            }),

        BuildTool("list_fuel_records",
            "Returns fuel issue records with vehicle fleet number, registration, fuel source (tank/offsite), litres, odometer, cost per litre, and total cost.",
            DateRangeProps()),

        BuildTool("list_slaughter_batches",
            "Returns slaughter batches showing input species/qty/cost and yield lines (output species, qty, unit cost, unit price).",
            DateRangeProps()),

        BuildTool("list_stock_losses",
            "Returns recorded stock losses with species name, quantity, adjustment type (Under/Over), recorded-by staff name, notes, and date.",
            DateRangeProps()),

        BuildTool("list_vehicles",
            "Returns fleet vehicles with fleet number, registration, make, model, fuel type, odometer, expected consumption, licence expiry, and service intervals.",
            new JsonObject()),

        BuildTool("list_procurement_orders",
            "Returns procurement orders placed with suppliers: supplier name, status, species lines with ordered qty and unit cost, and order totals.",
            new JsonObject
            {
                ["status"] = Prop("Filter by status: Draft, Confirmed, Received, Cancelled")
            }),

        BuildTool("list_driver_sales",
            "Returns individual driver stock sales (stock sold by drivers on their runs): driver name, species, qty, unit price, total, payment type, customer name, and date.",
            new JsonObject
            {
                ["from_date"]  = Prop("Start date YYYY-MM-DD"),
                ["to_date"]    = Prop("End date YYYY-MM-DD"),
                ["driver_id"]  = Prop("Filter to a specific driver by their user ID")
            }),

        BuildTool("list_dip_tank_readings",
            "Returns fuel dip tank definitions and recent dip readings showing litres remaining, percentage full, and who recorded the reading.",
            new JsonObject()),

        BuildTool("list_suppliers",
            "Returns all suppliers with name, city, province, contact person, and finance contact email.",
            new JsonObject()),
    };

    private static JsonObject Prop(string description) =>
        new() { ["type"] = "string", ["description"] = description };

    private static JsonObject DateRangeProps() => new()
    {
        ["from_date"] = Prop("Start date YYYY-MM-DD"),
        ["to_date"]   = Prop("End date YYYY-MM-DD")
    };

    private static JsonObject BuildTool(string name, string description, JsonObject properties) =>
        new()
        {
            ["name"]         = name,
            ["description"]  = description,
            ["input_schema"] = new JsonObject
            {
                ["type"]       = "object",
                ["properties"] = properties
            }
        };

    // ── Response parsing ────────────────────────────────────────────────────

    private static AiReportResult ParseFinalResponse(string text)
    {
        var clean = text.Trim();
        var start = clean.IndexOf('{');
        var end   = clean.LastIndexOf('}');
        if (start >= 0 && end > start)
            clean = clean[start..(end + 1)];

        try
        {
            var doc  = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            var narrative = root.TryGetProperty("narrative", out var n) ? n.GetString() ?? "" : clean;

            var columns = new List<string>();
            if (root.TryGetProperty("columns", out var cols))
                foreach (var c in cols.EnumerateArray())
                    columns.Add(c.GetString() ?? "");

            var rows = new List<List<string>>();
            if (root.TryGetProperty("rows", out var rowsEl))
                foreach (var row in rowsEl.EnumerateArray())
                {
                    var r = new List<string>();
                    foreach (var cell in row.EnumerateArray())
                        r.Add(cell.ValueKind == JsonValueKind.String ? cell.GetString()! : cell.ToString());
                    rows.Add(r);
                }

            return new AiReportResult { Narrative = narrative, Columns = columns, Rows = rows };
        }
        catch
        {
            return new AiReportResult { Narrative = text };
        }
    }
}
