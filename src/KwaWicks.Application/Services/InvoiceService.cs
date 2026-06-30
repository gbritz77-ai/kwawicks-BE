using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IDeliveryOrderRepository _deliveryRepo;
    private readonly IHubTaskRepository _hubTaskRepo;
    private readonly ISpeciesRepository _speciesRepo;
    private readonly IS3Service _s3Service;
    private readonly IPriceApprovalService _priceApproval;
    private readonly IClientCreditService _clientCreditService;

    public InvoiceService(
        IInvoiceRepository invoiceRepo,
        IDeliveryOrderRepository deliveryRepo,
        IHubTaskRepository hubTaskRepo,
        ISpeciesRepository speciesRepo,
        IS3Service s3Service,
        IPriceApprovalService priceApproval,
        IClientCreditService clientCreditService)
    {
        _invoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
        _deliveryRepo = deliveryRepo ?? throw new ArgumentNullException(nameof(deliveryRepo));
        _hubTaskRepo = hubTaskRepo ?? throw new ArgumentNullException(nameof(hubTaskRepo));
        _speciesRepo = speciesRepo ?? throw new ArgumentNullException(nameof(speciesRepo));
        _s3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
        _priceApproval = priceApproval ?? throw new ArgumentNullException(nameof(priceApproval));
        _clientCreditService = clientCreditService ?? throw new ArgumentNullException(nameof(clientCreditService));
    }

    // ── Hub-side: create invoice directly (existing flow) ──────────────────
    public async Task<string> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.CustomerId)) throw new ArgumentException("CustomerId is required.");
        if (string.IsNullOrWhiteSpace(request.HubId)) throw new ArgumentException("HubId is required.");
        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one invoice line is required.");

        foreach (var l in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(l.SpeciesId)) throw new ArgumentException("SpeciesId is required on all lines.");
            if (l.Quantity <= 0) throw new ArgumentException("Quantity must be greater than 0.");
            if (l.UnitPrice < 0) throw new ArgumentException("UnitPrice cannot be negative.");
            if (l.VatRate < 0) throw new ArgumentException("VatRate cannot be negative.");
        }

        var invoiceNumber = await _invoiceRepo.GetNextInvoiceNumberAsync(ct);

        var invoice = new Invoice
        {
            CustomerId = request.CustomerId,
            HubId = request.HubId,
            InvoiceNumber = invoiceNumber,
            PaymentType = request.PaymentType ?? "",
            SaleType = string.IsNullOrWhiteSpace(request.SaleType) ? "Delivery" : request.SaleType,
            StaffMemberId = request.StaffMemberId ?? "",
            Lines = new List<InvoiceLine>()
        };

        decimal subTotal = 0m;
        decimal vatTotal = 0m;
        var bookedOut = new List<(string speciesId, int qty)>();

        try
        {
            foreach (var line in request.Lines)
            {
                ct.ThrowIfCancellationRequested();

                var species = await _speciesRepo.GetAsync(line.SpeciesId, ct);
                if (species == null) throw new InvalidOperationException($"Species not found: {line.SpeciesId}");
                if (species.QtyOnHandHub < line.Quantity)
                    throw new InvalidOperationException(
                        $"Insufficient stock for {species.Name}. On hand: {species.QtyOnHandHub}, requested: {line.Quantity}");

                await _speciesRepo.AdjustStockAsync(line.SpeciesId, -line.Quantity, +line.Quantity, ct, minOnHandRequired: line.Quantity);
                bookedOut.Add((species.SpeciesId, line.Quantity));

                var lineNet = line.Quantity * line.UnitPrice;
                var lineVat = lineNet * line.VatRate;
                subTotal += lineNet;
                vatTotal += lineVat;

                invoice.Lines.Add(new InvoiceLine
                {
                    SpeciesId = line.SpeciesId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate,
                    LineTotal = lineNet + lineVat
                });
            }

            invoice.SubTotal = subTotal;
            invoice.VatTotal = vatTotal;
            invoice.GrandTotal = subTotal + vatTotal;

            // Validate and store split payments when PaymentType = "Split"
            if (request.PaymentType == "Split")
            {
                var splitLines = request.SplitPayments ?? new List<SplitPaymentLineRequest>();
                if (splitLines.Count == 0)
                    throw new ArgumentException("At least one split payment line is required when PaymentType is Split.");

                var validMethods = new[] { "Cash", "Card", "EFT" };
                foreach (var sp in splitLines)
                {
                    if (!validMethods.Contains(sp.Method))
                        throw new ArgumentException($"Invalid split payment method '{sp.Method}'. Valid values: Cash, Card, EFT.");
                    if (sp.Amount <= 0)
                        throw new ArgumentException("Each split payment amount must be greater than zero.");
                }

                var splitTotal = splitLines.Sum(sp => sp.Amount);
                if (Math.Abs(splitTotal - invoice.GrandTotal) > 0.05m)
                    throw new ArgumentException(
                        $"Split payment total ({splitTotal:F2}) does not match invoice total ({invoice.GrandTotal:F2}).");

                invoice.SplitPayments = splitLines.Select(sp => new Domain.Entities.SplitPayment
                {
                    Method = sp.Method,
                    Amount = sp.Amount
                }).ToList();
            }

            await _invoiceRepo.CreateAsync(invoice, ct);

            var deliveryOrder = new DeliveryOrder
            {
                InvoiceId = invoice.InvoiceId,
                HubId = request.HubId,
                CustomerId = request.CustomerId,
                DeliveryAddressLine1 = request.DeliveryAddressLine1 ?? "",
                City = request.City ?? "",
                Province = request.Province ?? "",
                PostalCode = request.PostalCode ?? "",
                Lines = invoice.Lines.Select(l => new DeliveryOrderLine
                {
                    SpeciesId = l.SpeciesId,
                    Quantity = l.Quantity
                }).ToList()
            };
            await _deliveryRepo.CreateAsync(deliveryOrder, ct);

            // Back-link the delivery order ID onto the invoice now that we have it
            invoice.DeliveryOrderId = deliveryOrder.DeliveryOrderId;
            await _invoiceRepo.UpdateAsync(invoice, ct);

            var hubTask = new HubTask
            {
                HubId = request.HubId,
                Type = "Invoice",
                Status = "Open",
                InvoiceId = invoice.InvoiceId,
                DeliveryOrderId = deliveryOrder.DeliveryOrderId,
                Title = $"Invoice {invoice.InvoiceId} - Pick & Pack"
            };
            await _hubTaskRepo.CreateAsync(hubTask, ct);

            return invoice.InvoiceId;
        }
        catch
        {
            foreach (var (speciesId, qty) in bookedOut)
            {
                try
                {
                    await _speciesRepo.AdjustStockAsync(speciesId, +qty, -qty, CancellationToken.None);
                }
                catch { /* swallow rollback errors */ }
            }
            throw;
        }
    }

    // ── Driver-side: create invoice from a delivery order ──────────────────
    public async Task<string> CreateFromDeliveryAsync(
        string deliveryOrderId,
        CreateInvoiceFromDeliveryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deliveryOrderId)) throw new ArgumentException("DeliveryOrderId is required.");
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one line is required.");

        var deliveryOrder = await _deliveryRepo.GetAsync(deliveryOrderId, ct)
            ?? throw new InvalidOperationException($"Delivery order not found: {deliveryOrderId}");

        if (deliveryOrder.Status != "OutForDelivery")
            throw new InvalidOperationException(
                $"Delivery order must be in OutForDelivery status to create an invoice. Current status: {deliveryOrder.Status}");

        // Validate that every request line has a matching delivery order line
        foreach (var rl in request.Lines)
        {
            var doLine = deliveryOrder.Lines.FirstOrDefault(l => l.SpeciesId == rl.SpeciesId)
                ?? throw new InvalidOperationException($"Species {rl.SpeciesId} is not on the delivery order.");

            var totalAccounted = rl.DeliveredQty + rl.ReturnedDeadQty + rl.ReturnedMutilatedQty + rl.ReturnedNotWantedQty;
            if (totalAccounted != doLine.Quantity)
                throw new InvalidOperationException(
                    $"For species {rl.SpeciesId}: delivered + returns ({totalAccounted}) must equal ordered quantity ({doLine.Quantity}).");
        }

        var invoiceNumber = await _invoiceRepo.GetNextInvoiceNumberAsync(ct);

        var invoice = new Invoice
        {
            CustomerId = deliveryOrder.CustomerId,
            HubId = deliveryOrder.HubId,
            DeliveryOrderId = deliveryOrderId,
            CreatedByDriverId = request.CreatedByDriverId,
            InvoiceNumber = invoiceNumber,
            PaymentStatus = "Pending",
            Lines = new List<InvoiceLine>()
        };

        decimal subTotal = 0m;
        decimal vatTotal = 0m;

        foreach (var line in request.Lines)
        {
            ct.ThrowIfCancellationRequested();

            if (line.DeliveredQty <= 0) continue; // no invoice line for zero-delivery items

            var lineNet = line.DeliveredQty * line.UnitPrice;
            var lineVat = lineNet * line.VatRate;
            subTotal += lineNet;
            vatTotal += lineVat;

            invoice.Lines.Add(new InvoiceLine
            {
                SpeciesId = line.SpeciesId,
                Quantity = line.DeliveredQty,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate,
                LineTotal = lineNet + lineVat
            });
        }

        invoice.SubTotal = subTotal;
        invoice.VatTotal = vatTotal;
        invoice.GrandTotal = subTotal + vatTotal;
        await _invoiceRepo.CreateAsync(invoice, ct);

        // Atomically reconcile stock: release booked qty, return not-wanted to on-hand
        foreach (var line in request.Lines)
        {
            var doLine = deliveryOrder.Lines.First(l => l.SpeciesId == line.SpeciesId);
            // Deduct from booked (negative delta) and add returns to on-hand
            await _speciesRepo.AdjustStockAsync(
                line.SpeciesId,
                onHandDelta: +line.ReturnedNotWantedQty,
                bookedDelta: -doLine.Quantity,
                ct);

            // Record return details on the delivery order line
            doLine.DeliveredQty = line.DeliveredQty;
            doLine.ReturnedDeadQty = line.ReturnedDeadQty;
            doLine.ReturnedMutilatedQty = line.ReturnedMutilatedQty;
            doLine.ReturnedNotWantedQty = line.ReturnedNotWantedQty;
        }

        // Link delivery order to invoice and mark as delivered
        deliveryOrder.InvoiceId = invoice.InvoiceId;
        deliveryOrder.Status = "Delivered";
        deliveryOrder.UpdatedAt = DateTime.UtcNow;
        await _deliveryRepo.UpdateAsync(deliveryOrder, ct);

        // Flag any lines sold below cost — fires WhatsApp alert and sets PriceApprovalStatus = "Pending"
        await _priceApproval.CheckAndFlagAsync(invoice.InvoiceId, ct);

        return invoice.InvoiceId;
    }

    // ── Get / List ──────────────────────────────────────────────────────────
    public async Task<InvoiceResponse?> GetAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct);
        return invoice == null ? null : MapToResponse(invoice);
    }

    public async Task<List<InvoiceResponse>> ListAsync(string? hubId, string? customerId, CancellationToken ct)
    {
        var invoices = await _invoiceRepo.ListAsync(hubId, customerId, ct);
        return invoices.Select(MapToResponse).ToList();
    }

    // ── Payment ─────────────────────────────────────────────────────────────
    public async Task RecordPaymentAsync(string invoiceId, RecordPaymentRequest request, CancellationToken ct)
    {
        var validTypes = new[] { "Cash", "EFT", "Credit", "CardMachine", "Split" };
        if (!validTypes.Contains(request.PaymentType))
            throw new ArgumentException($"Invalid PaymentType '{request.PaymentType}'. Valid values: {string.Join(", ", validTypes)}");

        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        invoice.PaymentType = request.PaymentType;
        invoice.UpdatedAt = DateTime.UtcNow;

        if (request.PaymentType == "Split")
        {
            var splitLines = request.SplitPayments ?? new List<SplitPaymentLineRequest>();
            if (splitLines.Count == 0)
                throw new ArgumentException("At least one split payment line is required when PaymentType is Split.");

            var validMethods = new[] { "Cash", "Card", "EFT", "CardMachine" };
            foreach (var sp in splitLines)
            {
                if (!validMethods.Contains(sp.Method))
                    throw new ArgumentException($"Invalid split payment method '{sp.Method}'. Valid values: Cash, Card, EFT, CardMachine.");
                if (sp.Amount <= 0)
                    throw new ArgumentException("Each split payment amount must be greater than zero.");
            }

            var splitTotal = splitLines.Sum(sp => sp.Amount);
            if (Math.Abs(splitTotal - invoice.GrandTotal) > 0.05m)
                throw new ArgumentException(
                    $"Split payment total ({splitTotal:F2}) does not match invoice total ({invoice.GrandTotal:F2}).");

            invoice.SplitPayments = splitLines.Select(sp => new Domain.Entities.SplitPayment
            {
                Method = sp.Method,
                Amount = sp.Amount
            }).ToList();
        }

        await _invoiceRepo.UpdateAsync(invoice, ct);

        // Cash and card-machine payments are collected by the driver in person and need no
        // further verification — confirm immediately so they don't sit as "Pending" forever.
        // Credit sales settle against the client's account immediately too — mark paid and
        // charge the credit ledger now instead of waiting for a separate confirm step.
        // EFT and Split are left Pending: EFT needs proof-of-payment review/reconciliation,
        // and Split may include an EFT leg that hasn't cleared yet.
        var autoConfirmTypes = new[] { "Cash", "CardMachine", "Credit" };
        if (autoConfirmTypes.Contains(request.PaymentType))
            await ConfirmPaymentAsync(invoiceId, ct);
    }

    public async Task ConfirmPaymentAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        invoice.PaymentStatus = "Paid";
        invoice.Status = "Paid";
        invoice.UpdatedAt = DateTime.UtcNow;

        // Every invoice posts to the client's credit ledger exactly once (guarded by LedgerCharged,
        // not PaymentStatus, so this can't double-fire if ConfirmPaymentAsync runs more than once).
        var needsLedgerPost = !invoice.LedgerCharged && !string.IsNullOrWhiteSpace(invoice.CustomerId);
        if (needsLedgerPost) invoice.LedgerCharged = true;

        await _invoiceRepo.UpdateAsync(invoice, ct);

        if (needsLedgerPost)
        {
            // Debit: the sale itself.
            await _clientCreditService.ChargeInvoiceAsync(invoice.CustomerId, invoiceId, invoice.GrandTotal, ct);

            // Credit: money actually received now, for every payment type except Credit
            // (Credit means the client owes it — no payment received yet).
            if (invoice.PaymentType != "Credit")
            {
                await _clientCreditService.RecordInvoicePaymentAsync(
                    invoice.CustomerId, invoiceId, invoice.GrandTotal, invoice.PaymentType, ct);
            }
        }
    }

    // ── EFT receipt upload ──────────────────────────────────────────────────
    public async Task<ReceiptUploadUrlResponse> GetReceiptUploadUrlAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        var key = $"receipts/{invoiceId}/{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        var url = await _s3Service.GeneratePresignedUploadUrlAsync(key, "image/jpeg", ct);

        // Persist the S3 key so it can be retrieved later
        invoice.ReceiptS3Key = key;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoiceRepo.UpdateAsync(invoice, ct);

        return new ReceiptUploadUrlResponse
        {
            PresignedUrl = url,
            S3Key = key,
            ExpiresAt = expiresAt
        };
    }

    // ── EFT receipt view ────────────────────────────────────────────────────
    public async Task<string> GetReceiptViewUrlAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        if (string.IsNullOrEmpty(invoice.ReceiptS3Key))
            throw new InvalidOperationException("No receipt has been uploaded for this invoice.");

        return await _s3Service.GeneratePresignedViewUrlAsync(invoice.ReceiptS3Key, ct);
    }

    // ── Owner: edit prices on an existing invoice ────────────────────────────
    public async Task<InvoiceResponse> UpdateLinesAsync(string invoiceId, UpdateInvoiceLinesRequest request, CancellationToken ct)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one line update is required.");

        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        foreach (var update in request.Lines)
        {
            var line = invoice.Lines.FirstOrDefault(l => l.SpeciesId == update.SpeciesId)
                ?? throw new InvalidOperationException($"Species {update.SpeciesId} is not on this invoice.");

            if (update.UnitPriceIncl < 0)
                throw new ArgumentException($"Unit price cannot be negative (species {update.SpeciesId}).");

            // Input is incl. VAT — back-calculate to ex-VAT for storage
            var exVatPrice = line.VatRate > 0
                ? update.UnitPriceIncl / (1 + line.VatRate)
                : update.UnitPriceIncl;

            line.UnitPrice = exVatPrice;
            line.LineTotal = line.Quantity * exVatPrice * (1 + line.VatRate);
        }

        // Recalculate totals across all lines (incl. lines not in the update)
        decimal subTotal = 0m, vatTotal = 0m;
        foreach (var line in invoice.Lines)
        {
            var net = line.Quantity * line.UnitPrice;
            subTotal += net;
            vatTotal += net * line.VatRate;
        }

        invoice.SubTotal = subTotal;
        invoice.VatTotal = vatTotal;
        invoice.GrandTotal = subTotal + vatTotal;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _invoiceRepo.UpdateAsync(invoice, ct);
        return MapToResponse(invoice);
    }

    // ── Reconciliation ──────────────────────────────────────────────────────
    public async Task<List<ReconInvoiceItem>> GetReconListAsync(
        string? paymentType, string? reconStatus, DateTime? from, DateTime? to, CancellationToken ct,
        decimal? amount = null)
    {
        var invoices = await _invoiceRepo.ListForReconAsync(paymentType, from, to, ct);

        invoices = reconStatus switch
        {
            "pending"    => invoices.Where(i => !i.ReconciledAt.HasValue).ToList(),
            "reconciled" => invoices.Where(i => i.ReconciledAt.HasValue).ToList(),
            _            => invoices
        };

        if (amount.HasValue)
            invoices = invoices.Where(i => i.GrandTotal == amount.Value).ToList();


        var now = DateTime.UtcNow;
        return invoices
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ReconInvoiceItem
            {
                InvoiceId       = i.InvoiceId,
                InvoiceNumber   = i.InvoiceNumber,
                CustomerId      = i.CustomerId,
                CustomerName    = "", // enriched by controller
                SaleType        = i.SaleType,
                PaymentType     = i.PaymentType,
                PaymentStatus   = i.PaymentStatus,
                GrandTotal       = i.GrandTotal,
                AmountPaid       = i.AmountPaid,
                AmountOutstanding = Math.Max(0m, i.GrandTotal - i.AmountPaid),
                IsPartiallyPaid  = i.AmountPaid > 0m && i.AmountPaid < i.GrandTotal,
                CancelledAt      = i.CancelledAt,
                CancelledReason  = i.CancelledReason,
                ReceiptS3Key    = i.ReceiptS3Key,
                CreatedAt       = i.CreatedAt,
                ReconReference  = i.ReconReference,
                ReconNotes      = i.ReconNotes,
                ReconciledAt    = i.ReconciledAt,
                DaysOutstanding = (int)(now - i.CreatedAt).TotalDays,
                SplitPayments   = i.SplitPayments.Select(s => new SplitPaymentLineResponse
                {
                    Method = s.Method,
                    Amount = s.Amount
                }).ToList()
            })
            .ToList();
    }

    public async Task ReconAsync(string invoiceId, ReconRequest request, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        var payment = request.Amount ?? invoice.GrandTotal;
        invoice.AmountPaid = Math.Min(invoice.AmountPaid + payment, invoice.GrandTotal);

        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber))
            invoice.ReconReference = request.ReferenceNumber;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            invoice.ReconNotes = request.Notes;

        // Mark fully reconciled only when invoice is fully paid
        if (invoice.AmountPaid >= invoice.GrandTotal)
        {
            invoice.ReconciledAt  = request.ReceivedAt ?? DateTime.UtcNow;
            invoice.PaymentStatus = "Paid";
            invoice.Status        = "Paid";
        }

        // First touch posts the sale (debit) once; every recon call posts the incremental
        // payment (credit) actually allocated this time — bank-matched, so tagged "EFT".
        var needsCharge = !invoice.LedgerCharged && !string.IsNullOrWhiteSpace(invoice.CustomerId);
        if (needsCharge) invoice.LedgerCharged = true;

        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoiceRepo.UpdateAsync(invoice, ct);

        if (!string.IsNullOrWhiteSpace(invoice.CustomerId))
        {
            if (needsCharge)
                await _clientCreditService.ChargeInvoiceAsync(invoice.CustomerId, invoiceId, invoice.GrandTotal, ct);

            if (payment > 0m)
                await _clientCreditService.RecordInvoicePaymentAsync(
                    invoice.CustomerId, invoiceId, payment, "EFT", ct, occurredAt: request.ReceivedAt);
        }
    }

    public async Task UnreconAsync(string invoiceId, decimal subtractAmount, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        invoice.AmountPaid = Math.Max(0m, invoice.AmountPaid - subtractAmount);

        if (invoice.AmountPaid < invoice.GrandTotal)
        {
            invoice.ReconciledAt  = null;
            invoice.PaymentStatus = "Pending";
            invoice.Status        = invoice.Status == "Paid" ? "Confirmed" : invoice.Status;
        }

        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoiceRepo.UpdateAsync(invoice, ct);

        // Undo the matching payment (credit) entry — the sale/charge itself still stands,
        // since the goods were still delivered; only the "money received" record is reversed.
        if (subtractAmount > 0m && !string.IsNullOrWhiteSpace(invoice.CustomerId))
            await _clientCreditService.ReverseInvoicePaymentAsync(invoice.CustomerId, invoiceId, subtractAmount, ct);
    }

    // ── Cancellation ────────────────────────────────────────────────────────
    public async Task CancelInvoiceAsync(string invoiceId, string reason, string cancelledByUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A cancellation reason is required.");

        var invoice = await _invoiceRepo.GetAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice not found: {invoiceId}");

        if (invoice.Status == "Cancelled")
            throw new InvalidOperationException("This invoice is already cancelled.");

        if (invoice.AmountPaid > 0m)
            throw new InvalidOperationException(
                "This invoice has bank-reconciled payment(s). Remove the bank allocation(s) on the Reconciliation page before cancelling.");

        // Restore stock booked out at creation time
        foreach (var line in invoice.Lines)
        {
            try
            {
                await _speciesRepo.AdjustStockAsync(line.SpeciesId, +line.Quantity, -line.Quantity, ct);
            }
            catch
            {
                // Species may have been deleted; don't block cancellation on a stock restore failure.
            }
        }

        // Reverse the sale (debit) and, if money was actually received, the matching payment
        // (credit) too — both were posted together by ConfirmPaymentAsync/ReconAsync.
        if (invoice.LedgerCharged && !string.IsNullOrWhiteSpace(invoice.CustomerId))
        {
            await _clientCreditService.ReverseInvoiceChargeAsync(invoice.CustomerId, invoiceId, invoice.GrandTotal, ct);
            if (invoice.PaymentType != "Credit")
                await _clientCreditService.ReverseInvoicePaymentAsync(invoice.CustomerId, invoiceId, invoice.GrandTotal, ct);
        }

        invoice.Status            = "Cancelled";
        invoice.CancelledAt       = DateTime.UtcNow;
        invoice.CancelledReason   = reason.Trim();
        invoice.CancelledByUserId = cancelledByUserId;
        invoice.UpdatedAt         = DateTime.UtcNow;

        await _invoiceRepo.UpdateAsync(invoice, ct);
    }

    private static InvoiceResponse MapToResponse(Invoice invoice) => new()
    {
        InvoiceId = invoice.InvoiceId,
        InvoiceNumber = invoice.InvoiceNumber,
        SaleType = invoice.SaleType,
        StaffMemberId = invoice.StaffMemberId,
        CustomerId = invoice.CustomerId,
        HubId = invoice.HubId,
        DeliveryOrderId = invoice.DeliveryOrderId,
        CreatedByDriverId = invoice.CreatedByDriverId,
        Status = invoice.Status,
        PaymentType = invoice.PaymentType,
        PaymentStatus = invoice.PaymentStatus,
        ReceiptS3Key = invoice.ReceiptS3Key,
        SubTotal          = invoice.SubTotal,
        VatTotal          = invoice.VatTotal,
        GrandTotal        = invoice.GrandTotal,
        AmountPaid        = invoice.AmountPaid,
        AmountOutstanding = Math.Max(0m, invoice.GrandTotal - invoice.AmountPaid),
        CreatedAt         = invoice.CreatedAt,
        UpdatedAt         = invoice.UpdatedAt,
        ReconReference    = invoice.ReconReference,
        ReconNotes        = invoice.ReconNotes,
        ReconciledAt      = invoice.ReconciledAt,
        CancelledAt       = invoice.CancelledAt,
        CancelledReason   = invoice.CancelledReason,
        CancelledByUserId = invoice.CancelledByUserId,
        Lines = invoice.Lines.Select(l => new InvoiceLineResponse
        {
            SpeciesId = l.SpeciesId,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            VatRate = l.VatRate,
            LineTotal = l.LineTotal
        }).ToList(),
        SplitPayments = invoice.SplitPayments.Select(sp => new SplitPaymentLineResponse
        {
            Method = sp.Method,
            Amount = sp.Amount
        }).ToList()
    };
}
