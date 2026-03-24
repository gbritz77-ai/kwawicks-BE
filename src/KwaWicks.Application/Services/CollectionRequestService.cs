using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class CollectionRequestService : ICollectionRequestService
{
    private readonly ICollectionRequestRepository _repo;
    private readonly IProcurementOrderRepository _poRepo;
    private readonly ISpeciesRepository _speciesRepo;
    private readonly IS3Service _s3;

    public CollectionRequestService(
        ICollectionRequestRepository repo,
        IProcurementOrderRepository poRepo,
        ISpeciesRepository speciesRepo,
        IS3Service s3)
    {
        _repo = repo;
        _poRepo = poRepo;
        _speciesRepo = speciesRepo;
        _s3 = s3;
    }

    public async Task<CollectionRequestResponse> CreateAsync(CreateCollectionRequestRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProcurementOrderId)) throw new ArgumentException("ProcurementOrderId is required.");
        if (string.IsNullOrWhiteSpace(request.AssignedDriverId)) throw new ArgumentException("AssignedDriverId is required.");

        var po = await _poRepo.GetAsync(request.ProcurementOrderId, ct)
            ?? throw new InvalidOperationException($"Procurement order not found: {request.ProcurementOrderId}");

        if (po.Status != "Submitted" && po.Status != "CollectionScheduled")
            throw new InvalidOperationException($"Procurement order must be Submitted or CollectionScheduled to create a collection request. Current status: {po.Status}");

        var cr = new CollectionRequest
        {
            ProcurementOrderId = request.ProcurementOrderId,
            SupplierId = po.SupplierId,
            SupplierName = po.SupplierName,
            AssignedDriverId = request.AssignedDriverId,
            AssignedDriverName = request.AssignedDriverName ?? "",
            HubId = request.HubId ?? "hub-001",
            Notes = request.Notes ?? "",
            CollectionDate = request.CollectionDate,
            Status = "Pending",
            Lines = po.Lines.Select(l => new CollectionRequestLine
            {
                SpeciesId = l.SpeciesId,
                SpeciesName = l.SpeciesName,
                OrderedQty = l.OrderedQty,
                LoadedQty = 0,
                LoadingNotes = "",
                ReceivedQty = 0,
                DiscrepancyNotes = ""
            }).ToList()
        };

        await _repo.CreateAsync(cr, ct);

        // Advance PO to CollectionScheduled
        if (po.Status == "Submitted")
        {
            po.Status = "CollectionScheduled";
            await _poRepo.UpdateAsync(po, ct);
        }

        return MapToResponse(cr);
    }

    public async Task<CollectionRequestResponse?> GetAsync(string id, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct);
        return cr == null ? null : MapToResponse(cr);
    }

    public async Task<List<CollectionRequestResponse>> ListAsync(string? driverId = null, string? status = null, string? procurementOrderId = null, CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(driverId, status, procurementOrderId, ct);
        return items.Select(MapToResponse).ToList();
    }

    public async Task<CollectionRequestResponse> DriverLoadAsync(string id, DriverLoadingUpdateRequest request, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        if (cr.Status != "Pending" && cr.Status != "Loading")
            throw new InvalidOperationException($"Cannot update loading for status: {cr.Status}");

        foreach (var update in request.Lines)
        {
            var line = cr.Lines.FirstOrDefault(l => l.SpeciesId == update.SpeciesId);
            if (line != null)
            {
                line.LoadedQty = update.LoadedQty;
                line.LoadingNotes = update.LoadingNotes ?? "";
            }
        }

        cr.Status = "Loading";
        await _repo.UpdateAsync(cr, ct);
        return MapToResponse(cr);
    }

    public async Task<CollectionRequestResponse> DispatchAsync(string id, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        if (cr.Status != "Loading" && cr.Status != "Pending")
            throw new InvalidOperationException($"Cannot dispatch from status: {cr.Status}");

        cr.Status = "InTransit";
        await _repo.UpdateAsync(cr, ct);

        // Advance PO to InTransit
        var po = await _poRepo.GetAsync(cr.ProcurementOrderId, ct);
        if (po != null && po.Status == "CollectionScheduled")
        {
            po.Status = "InTransit";
            await _poRepo.UpdateAsync(po, ct);
        }

        return MapToResponse(cr);
    }

    public async Task<CollectionRequestResponse> ArriveAsync(string id, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        if (cr.Status != "InTransit")
            throw new InvalidOperationException($"Cannot mark arrived from status: {cr.Status}");

        cr.Status = "ArrivedAtHub";
        await _repo.UpdateAsync(cr, ct);
        return MapToResponse(cr);
    }

    public async Task<CollectionRequestResponse> HubConfirmAsync(string id, HubConfirmReceiptRequest request, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        if (cr.Status != "ArrivedAtHub" && cr.Status != "InTransit")
            throw new InvalidOperationException($"Cannot confirm receipt from status: {cr.Status}");

        // Guard against double-confirmation
        if (cr.Status == "HubConfirmed")
            throw new InvalidOperationException("Collection request is already confirmed.");

        // Update received quantities
        foreach (var update in request.Lines)
        {
            var line = cr.Lines.FirstOrDefault(l => l.SpeciesId == update.SpeciesId);
            if (line != null)
            {
                line.ReceivedQty = update.ReceivedQty;
                line.DiscrepancyNotes = update.DiscrepancyNotes ?? "";
            }
        }

        cr.Status = "HubConfirmed";

        // Book stock into hub with compensating rollback
        var bookedIn = new List<(string speciesId, int qty)>();
        try
        {
            foreach (var line in cr.Lines.Where(l => l.ReceivedQty > 0))
            {
                ct.ThrowIfCancellationRequested();
                var species = await _speciesRepo.GetAsync(line.SpeciesId, ct);
                if (species != null)
                {
                    species.QtyOnHandHub += line.ReceivedQty;
                    await _speciesRepo.UpdateAsync(species, ct);
                    bookedIn.Add((line.SpeciesId, line.ReceivedQty));
                }
            }

            await _repo.UpdateAsync(cr, ct);

            // Advance PO to DeliveredToHub
            var po = await _poRepo.GetAsync(cr.ProcurementOrderId, ct);
            if (po != null && (po.Status == "InTransit" || po.Status == "CollectionScheduled"))
            {
                po.Status = "DeliveredToHub";
                await _poRepo.UpdateAsync(po, ct);
            }
        }
        catch
        {
            // Compensating rollback
            foreach (var (speciesId, qty) in bookedIn)
            {
                try
                {
                    var s = await _speciesRepo.GetAsync(speciesId, ct);
                    if (s != null)
                    {
                        s.QtyOnHandHub = Math.Max(0, s.QtyOnHandHub - qty);
                        await _speciesRepo.UpdateAsync(s, ct);
                    }
                }
                catch { /* swallow rollback errors */ }
            }
            throw;
        }

        return MapToResponse(cr);
    }

    public async Task<CollectionRequestResponse> FinanceAcknowledgeAsync(string id, string invoiceS3Key, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        if (cr.Status != "HubConfirmed")
            throw new InvalidOperationException($"Collection request must be HubConfirmed to acknowledge. Current: {cr.Status}");

        cr.Status = "FinanceAcknowledged";
        cr.InvoiceS3Key = invoiceS3Key ?? cr.InvoiceS3Key;
        await _repo.UpdateAsync(cr, ct);

        // Complete the PO
        var po = await _poRepo.GetAsync(cr.ProcurementOrderId, ct);
        if (po != null && po.Status == "DeliveredToHub")
        {
            po.Status = "Completed";
            await _poRepo.UpdateAsync(po, ct);
        }

        return MapToResponse(cr);
    }

    public async Task<CollectionInvoiceUploadUrlResponse> GetInvoiceUploadUrlAsync(string id, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        var key = $"collection/invoices/{id}/{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var url = await _s3.GeneratePresignedUploadUrlAsync(key, "application/pdf", ct);

        cr.InvoiceS3Key = key;
        await _repo.UpdateAsync(cr, ct);

        return new CollectionInvoiceUploadUrlResponse { UploadUrl = url, S3Key = key };
    }

    public async Task<string> GetDeliveryNoteViewUrlAsync(string id, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        if (string.IsNullOrWhiteSpace(cr.DeliveryNoteS3Key))
            throw new InvalidOperationException("No delivery note has been uploaded for this collection request.");

        return await _s3.GeneratePresignedViewUrlAsync(cr.DeliveryNoteS3Key, 15, ct);
    }

    public async Task<CollectionInvoiceUploadUrlResponse> GetDeliveryNoteUploadUrlAsync(string id, CancellationToken ct = default)
    {
        var cr = await _repo.GetAsync(id, ct)
            ?? throw new InvalidOperationException($"Collection request not found: {id}");

        var key = $"collection/delivery-notes/{id}/{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";
        var url = await _s3.GeneratePresignedUploadUrlAsync(key, "image/jpeg", ct);

        cr.DeliveryNoteS3Key = key;
        await _repo.UpdateAsync(cr, ct);

        return new CollectionInvoiceUploadUrlResponse { UploadUrl = url, S3Key = key };
    }

    private static CollectionRequestResponse MapToResponse(CollectionRequest cr) => new()
    {
        CollectionRequestId = cr.CollectionRequestId,
        ProcurementOrderId = cr.ProcurementOrderId,
        SupplierId = cr.SupplierId,
        SupplierName = cr.SupplierName,
        AssignedDriverId = cr.AssignedDriverId,
        AssignedDriverName = cr.AssignedDriverName,
        HubId = cr.HubId,
        Status = cr.Status,
        Notes = cr.Notes,
        CollectionDate = cr.CollectionDate,
        InvoiceS3Key = cr.InvoiceS3Key,
        DeliveryNoteS3Key = cr.DeliveryNoteS3Key,
        CreatedAt = cr.CreatedAt,
        UpdatedAt = cr.UpdatedAt,
        Lines = cr.Lines.Select(l => new CollectionRequestLineResponse
        {
            SpeciesId = l.SpeciesId,
            SpeciesName = l.SpeciesName,
            OrderedQty = l.OrderedQty,
            LoadedQty = l.LoadedQty,
            LoadingNotes = l.LoadingNotes,
            ReceivedQty = l.ReceivedQty,
            DiscrepancyNotes = l.DiscrepancyNotes
        }).ToList()
    };
}
