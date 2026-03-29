using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class HubRequestService : IHubRequestService
{
    private readonly IHubRequestRepository _repo;
    private readonly IWhatsAppService _whatsApp;
    private readonly ISettingsRepository _settings;

    public HubRequestService(IHubRequestRepository repo, IWhatsAppService whatsApp, ISettingsRepository settings)
    {
        _repo = repo;
        _whatsApp = whatsApp;
        _settings = settings;
    }

    public async Task<HubRequestDto> CreateAsync(CreateHubRequestRequest request, string requestedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.");

        var entity = new HubRequest
        {
            RequestedBy = requestedBy,
            Message = request.Message.Trim(),
        };

        // Hub number: prefer DB setting, fall back to env var
        var dbSettings = await _settings.GetAsync(ct);
        var hubPhone = dbSettings?.HubWhatsAppNumber?.Trim()
                       ?? Environment.GetEnvironmentVariable("HUB_WHATSAPP_NUMBER")
                       ?? "";
        if (!string.IsNullOrWhiteSpace(hubPhone))
        {
            try
            {
                await _whatsApp.SendHubRequestAsync(hubPhone, requestedBy, entity.Message, ct);
            }
            catch (Exception ex)
            {
                entity.WhatsAppError = ex.Message;
            }
        }

        await _repo.CreateAsync(entity, ct);
        return Map(entity);
    }

    public async Task<List<HubRequestDto>> ListAsync(string? status, CancellationToken ct)
    {
        var items = await _repo.ListAsync(status, ct);
        return items
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(Map)
            .ToList();
    }

    public async Task<HubRequestDto> GetAsync(string hubRequestId, CancellationToken ct)
    {
        var entity = await _repo.GetAsync(hubRequestId, ct)
                     ?? throw new KeyNotFoundException("Hub request not found.");
        return Map(entity);
    }

    public async Task<HubRequestDto> ActionAsync(string hubRequestId, ActionHubRequestRequest request, string actionedBy, CancellationToken ct)
    {
        var entity = await _repo.GetAsync(hubRequestId, ct)
                     ?? throw new KeyNotFoundException("Hub request not found.");

        if (entity.Status != "Pending")
            throw new InvalidOperationException($"Request is already {entity.Status}.");

        entity.Status = "Actioned";
        entity.ActionedBy = actionedBy;
        entity.ActionNotes = request.ActionNotes?.Trim() ?? "";
        entity.LinkedOrderId = request.LinkedOrderId?.Trim() ?? "";
        entity.LinkedOrderType = request.LinkedOrderType?.Trim() ?? "";
        entity.LinkedOrderRef = request.LinkedOrderRef?.Trim() ?? "";
        entity.ActionedAtUtc = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);
        return Map(entity);
    }

    public async Task<HubRequestDto> CancelAsync(string hubRequestId, string cancelledBy, CancellationToken ct)
    {
        var entity = await _repo.GetAsync(hubRequestId, ct)
                     ?? throw new KeyNotFoundException("Hub request not found.");

        if (entity.Status != "Pending")
            throw new InvalidOperationException($"Request is already {entity.Status}.");

        entity.Status = "Cancelled";
        entity.ActionedBy = cancelledBy;
        entity.ActionedAtUtc = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);
        return Map(entity);
    }

    private static HubRequestDto Map(HubRequest r) => new()
    {
        HubRequestId = r.HubRequestId,
        RequestedBy = r.RequestedBy,
        Message = r.Message,
        Status = r.Status,
        ActionedBy = r.ActionedBy,
        ActionNotes = r.ActionNotes,
        LinkedOrderId = r.LinkedOrderId,
        LinkedOrderType = r.LinkedOrderType,
        LinkedOrderRef = r.LinkedOrderRef,
        WhatsAppError = string.IsNullOrEmpty(r.WhatsAppError) ? null : r.WhatsAppError,
        CreatedAtUtc = r.CreatedAtUtc,
        ActionedAtUtc = r.ActionedAtUtc,
    };
}
