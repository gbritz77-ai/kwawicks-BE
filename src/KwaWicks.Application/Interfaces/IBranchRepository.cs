using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IBranchRepository
{
    Task<List<Branch>> ListAsync(CancellationToken ct);
    Task<Branch?> GetAsync(string branchId, CancellationToken ct);
    Task CreateAsync(Branch branch, CancellationToken ct);
    Task UpdateAsync(Branch branch, CancellationToken ct);
    Task DeleteAsync(string branchId, CancellationToken ct);

    Task<List<BranchUser>> ListUsersAsync(string branchId, CancellationToken ct);
    Task<BranchUser?> GetUserByUsernameAsync(string username, CancellationToken ct);
    Task CreateUserAsync(BranchUser user, CancellationToken ct);
    Task UpdateUserAsync(BranchUser user, CancellationToken ct);
    Task DeleteUserAsync(string branchId, string branchUserId, CancellationToken ct);
}
