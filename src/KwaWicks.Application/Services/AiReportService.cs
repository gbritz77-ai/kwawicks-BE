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

    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public AiReportService(
        HttpClient http,
        IInvoiceRepository invoices,
        IClientRepository clients,
        IClientCreditRepository credits,
        ICollectionRequestRepository collections,
        IStaffMemberRepository staff,
        IPettyCashService pettyCash,
        ISpeciesRepository species)
    {
        _http = http;
        _invoices = invoices;
        _clients = clients;
        _credits = credits;
        _collections = collections;
        _staff = staff;
        _pettyCash = pettyCash;
        _species = species;
    }

    public async Task<AiReportResult> RunReportAsync(string prompt, CancellationToken ct)
    {
        var toolsJson    = BuildTools().ToJsonString();
        var messagesJson = new System.Text.Json.Nodes.JsonArray
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
            "Amounts: South African Rand format e.g. 'R 1 234,56'. Dates: YYYY-MM-DD. " +
            "If no tabular data applies leave columns and rows as empty arrays. " +
            "IMPORTANT: Your entire response must be parseable JSON starting with { and ending with }.";

        // Agentic loop — max 5 rounds to avoid runaway
        for (var round = 0; round < 5; round++)
        {
            // Re-parse each iteration — JsonNode children can only have one parent
            var requestBody = new JsonObject
            {
                ["model"]      = "claude-haiku-4-5-20251001",
                ["max_tokens"] = 4096,
                ["system"]     = systemPrompt,
                ["tools"]      = JsonNode.Parse(toolsJson),
                ["messages"]   = JsonNode.Parse(messagesJson)
            };

            var response = await _http.PostAsync(
                "https://api.anthropic.com/v1/messages",
                new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json"),
                ct);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct)
                       ?? throw new InvalidOperationException("Empty response from Anthropic API");

            var stopReason  = body["stop_reason"]?.GetValue<string>();
            var contentJson = body["content"]?.ToJsonString() ?? "[]";
            var content     = JsonNode.Parse(contentJson)!.AsArray();

            // Collect tool_use blocks
            var toolUseBlocks = content
                .OfType<JsonObject>()
                .Where(b => b["type"]?.GetValue<string>() == "tool_use")
                .ToList();

            // Append assistant turn to messages JSON string
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

            // Execute tool calls
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
                "list_clients"               => await ListClientsAsync(ct),
                "get_outstanding_balances"   => await GetOutstandingBalancesAsync(ct),
                "get_sales_summary"          => await GetSalesSummaryAsync(input, ct),
                "list_invoices"              => await ListInvoicesAsync(input, ct),
                "get_invoice_line_items"     => await GetInvoiceLineItemsAsync(input, ct),
                "get_petty_cash_summary"     => await GetPettyCashSummaryAsync(ct),
                "list_collections"           => await ListCollectionsAsync(input, ct),
                "list_staff_members"         => await ListStaffMembersAsync(ct),
                _                            => $"Unknown tool: {toolName}"
            };
        }
        catch (Exception ex)
        {
            return $"Error executing {toolName}: {ex.Message}";
        }
    }

    private async Task<string> ListClientsAsync(CancellationToken ct)
    {
        var clients = await _clients.ListAsync(200, ct);
        var rows = clients.Select(c => new { c.ClientId, c.ClientName, c.ClientPhone, c.ClientType, c.IsWalkIn }).ToList();
        return JsonSerializer.Serialize(rows);
    }

    private async Task<string> GetOutstandingBalancesAsync(CancellationToken ct)
    {
        var clients  = await _clients.ListAsync(200, ct);
        var allInvoices = await _invoices.ListAsync(null, null, ct);

        var result = clients.Select(c =>
        {
            var clientInvoices = allInvoices
                .Where(i => i.CustomerId == c.ClientId && i.Status != "Cancelled")
                .ToList();
            var totalBilled = clientInvoices.Sum(i => i.GrandTotal);
            var totalPaid   = clientInvoices.Sum(i => i.AmountPaid);
            var outstanding = totalBilled - totalPaid;

            return new
            {
                ClientName  = c.ClientName,
                TotalBilled = totalBilled,
                TotalPaid   = totalPaid,
                Outstanding = outstanding,
                InvoiceCount = clientInvoices.Count
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

        var allInvoices = await _invoices.ListAsync(null, null, ct);

        var filtered = allInvoices.Where(i => i.Status != "Cancelled");
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDt))
            filtered = filtered.Where(i => i.CreatedAt >= fromDt);
        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDt))
            filtered = filtered.Where(i => i.CreatedAt <= toDt.AddDays(1));

        var list = filtered.ToList();

        var result = new
        {
            TotalInvoices   = list.Count,
            TotalRevenue    = list.Sum(i => i.GrandTotal),
            TotalCash       = list.Where(i => i.PaymentType == "Cash").Sum(i => i.GrandTotal),
            TotalEft        = list.Where(i => i.PaymentType == "EFT").Sum(i => i.GrandTotal),
            TotalCredit     = list.Where(i => i.PaymentType == "Credit").Sum(i => i.GrandTotal),
            TotalPaid       = list.Sum(i => i.AmountPaid),
            TotalOutstanding = list.Sum(i => i.GrandTotal - i.AmountPaid),
            ByDate = list
                .GroupBy(i => i.CreatedAt.ToString("yyyy-MM-dd"))
                .Select(g => new { Date = g.Key, Revenue = g.Sum(i => i.GrandTotal), Count = g.Count() })
                .OrderByDescending(g => g.Date)
                .Take(30)
                .ToList()
        };

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> ListInvoicesAsync(JsonObject input, CancellationToken ct)
    {
        var status      = input["status"]?.GetValue<string>();
        var paymentType = input["payment_type"]?.GetValue<string>();
        var from        = input["from_date"]?.GetValue<string>();
        var to          = input["to_date"]?.GetValue<string>();

        var allInvoices = await _invoices.ListAsync(null, null, ct);
        var filtered = allInvoices.AsEnumerable();

        if (!string.IsNullOrEmpty(status))
            filtered = filtered.Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(paymentType))
            filtered = filtered.Where(i => string.Equals(i.PaymentType, paymentType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDt))
            filtered = filtered.Where(i => i.CreatedAt >= fromDt);
        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDt))
            filtered = filtered.Where(i => i.CreatedAt <= toDt.AddDays(1));

        var rows = filtered
            .OrderByDescending(i => i.CreatedAt)
            .Take(100)
            .Select(i => new
            {
                i.InvoiceNumber,
                i.CustomerId,
                CreatedAt   = i.CreatedAt.ToString("yyyy-MM-dd"),
                i.Status,
                i.PaymentType,
                i.PaymentStatus,
                GrandTotal  = i.GrandTotal,
                AmountPaid  = i.AmountPaid,
                Outstanding = i.GrandTotal - i.AmountPaid
            })
            .ToList();

        return JsonSerializer.Serialize(rows);
    }

    private async Task<string> GetPettyCashSummaryAsync(CancellationToken ct)
    {
        var summary = await _pettyCash.GetSummaryAsync(ct);
        return JsonSerializer.Serialize(new
        {
            summary.CurrentBalance,
            summary.TotalInSinceLastCashup,
            summary.TotalOutSinceLastCashup,
            summary.OpenEntryCount,
            summary.LastCashupDate,
            summary.CashFromHubSales,
            summary.CashFromCreditDeposits,
            summary.TotalCashInCustody,
            OpenEntries = summary.OpenEntries.Select(e => new
            {
                e.EntryDate, e.Type, e.Category, e.Description, e.Amount, e.RecipientName
            })
        });
    }

    private async Task<string> ListCollectionsAsync(JsonObject input, CancellationToken ct)
    {
        var status = input["status"]?.GetValue<string>();
        var all    = await _collections.ListAsync(status: status, ct: ct);

        var rows = all
            .OrderByDescending(c => c.CreatedAt)
            .Take(100)
            .Select(c => new
            {
                c.CollectionRequestId,
                c.SupplierName,
                c.AssignedDriverName,
                c.Status,
                Date      = c.CollectionDate?.ToString("yyyy-MM-dd") ?? c.CreatedAt.ToString("yyyy-MM-dd"),
                TotalOrdered = c.Lines.Sum(l => l.OrderedQty),
                TotalLoaded  = c.Lines.Sum(l => l.LoadedQty),
                TotalDead    = c.Lines.Sum(l => l.DeadQty),
                TotalShort   = c.Lines.Sum(l => Math.Max(0, l.OrderedQty - l.LoadedQty)),
                TotalOver    = c.Lines.Sum(l => Math.Max(0, l.LoadedQty - l.OrderedQty))
            })
            .ToList();

        return JsonSerializer.Serialize(rows);
    }

    private async Task<string> ListStaffMembersAsync(CancellationToken ct)
    {
        var staff = await _staff.ListAsync(ct);
        var rows = staff.Select(s => new
        {
            s.StaffMemberId,
            s.Name,
            s.Department,
            s.Phone,
            s.IsActive
        }).ToList();
        return JsonSerializer.Serialize(rows);
    }

    private async Task<string> GetInvoiceLineItemsAsync(JsonObject input, CancellationToken ct)
    {
        var from       = input["from_date"]?.GetValue<string>();
        var to         = input["to_date"]?.GetValue<string>();
        var customerId = input["customer_id"]?.GetValue<string>();

        var allInvoices = await _invoices.ListAsync(null, null, ct);
        var allSpecies  = await _species.ListAsync(ct);
        var allClients  = await _clients.ListAsync(200, ct);

        var speciesMap = allSpecies.ToDictionary(s => s.SpeciesId, s => s.Name);
        var clientMap  = allClients.ToDictionary(c => c.ClientId, c => c.ClientName);

        var filtered = allInvoices.Where(i => i.Status != "Cancelled");
        if (!string.IsNullOrEmpty(customerId))
            filtered = filtered.Where(i => i.CustomerId == customerId);
        if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var fromDt))
            filtered = filtered.Where(i => i.CreatedAt >= fromDt);
        if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var toDt))
            filtered = filtered.Where(i => i.CreatedAt <= toDt.AddDays(1));

        var rows = filtered
            .OrderByDescending(i => i.CreatedAt)
            .Take(500)
            .SelectMany(i => i.Lines.Select(l => new
            {
                InvoiceNumber = i.InvoiceNumber,
                Date          = i.CreatedAt.ToString("yyyy-MM-dd"),
                CustomerName  = clientMap.TryGetValue(i.CustomerId, out var cn) ? cn : i.CustomerId,
                PaymentType   = i.PaymentType,
                SpeciesName   = speciesMap.TryGetValue(l.SpeciesId, out var sn) ? sn : l.SpeciesId,
                Quantity      = l.Quantity,
                UnitPrice     = l.UnitPrice,
                LineTotal     = l.LineTotal
            }))
            .ToList();

        return JsonSerializer.Serialize(rows);
    }

    // ── Tool definitions ────────────────────────────────────────────────────

    private static JsonArray BuildTools() => new()
    {
        BuildTool("list_clients",
            "Returns a list of all clients (customers) registered in the system with their names and contact details.",
            new JsonObject()),

        BuildTool("get_outstanding_balances",
            "Returns all clients with their total billed amount, total paid, and outstanding balance. Use this for accounts receivable / debtors reports.",
            new JsonObject()),

        BuildTool("get_sales_summary",
            "Returns a sales summary including total revenue, breakdown by payment type (Cash/EFT/Credit), and daily totals. Optionally filter by date range.",
            new JsonObject
            {
                ["from_date"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "Start date in YYYY-MM-DD format (optional)"
                },
                ["to_date"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "End date in YYYY-MM-DD format (optional)"
                }
            }),

        BuildTool("get_invoice_line_items",
            "Returns individual invoice line items with species name, quantity, unit price, and line total. Use this when the user asks about which species were purchased, quantities per species, or species-level breakdowns by customer or payment method. Returns up to 500 lines across all matching invoices.",
            new JsonObject
            {
                ["from_date"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "Start date in YYYY-MM-DD format (optional)"
                },
                ["to_date"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "End date in YYYY-MM-DD format (optional)"
                },
                ["customer_id"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "Filter to a specific customer by their ClientId (optional)"
                }
            }),

        BuildTool("list_invoices",
            "Lists invoices with optional filters. Returns up to 100 most recent invoices.",
            new JsonObject
            {
                ["status"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "Filter by status: Draft, Confirmed, Cancelled, Paid (optional)"
                },
                ["payment_type"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "Filter by payment type: Cash, EFT, Credit (optional)"
                },
                ["from_date"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "Start date in YYYY-MM-DD format (optional)"
                },
                ["to_date"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "End date in YYYY-MM-DD format (optional)"
                }
            }),

        BuildTool("get_petty_cash_summary",
            "Returns the current petty cash state including cash in custody, hub sales cash, client deposits, open entries, and last cashup date.",
            new JsonObject()),

        BuildTool("list_collections",
            "Returns collection requests (procurement collections from suppliers) with dead, short, and over quantities.",
            new JsonObject
            {
                ["status"] = new JsonObject
                {
                    ["type"]        = "string",
                    ["description"] = "Filter by status: Pending, Loading, InTransit, ArrivedAtHub, HubConfirmed, FinanceAcknowledged (optional)"
                }
            }),

        BuildTool("list_staff_members",
            "Returns all staff members with their roles and current stock deduction balances.",
            new JsonObject())
    };

    private static JsonObject BuildTool(string name, string description, JsonObject properties)
    {
        return new JsonObject
        {
            ["name"]        = name,
            ["description"] = description,
            ["input_schema"] = new JsonObject
            {
                ["type"]       = "object",
                ["properties"] = properties
            }
        };
    }

    // ── Response parsing ────────────────────────────────────────────────────

    private static AiReportResult ParseFinalResponse(string text)
    {
        // Find the outermost JSON object anywhere in the response
        var clean = text.Trim();
        var start = clean.IndexOf('{');
        var end   = clean.LastIndexOf('}');
        if (start >= 0 && end > start)
            clean = clean[start..(end + 1)];

        try
        {
            var doc = JsonDocument.Parse(clean);
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
