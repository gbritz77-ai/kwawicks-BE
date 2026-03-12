using System.Threading;
using System.Threading.Tasks;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IHubTaskRepository
{
    Task<HubTask> CreateAsync(HubTask task, CancellationToken ct);
    Task<HubTask?> GetAsync(string hubTaskId, CancellationToken ct);
    Task UpdateStatusAsync(string hubTaskId, string status, CancellationToken ct);
}