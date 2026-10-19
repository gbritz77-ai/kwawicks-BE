using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IBranchService
{
    Task<List<BranchDto>> ListAsync(CancellationToken ct);
    Task<BranchDto> GetAsync(string branchId, CancellationToken ct);
    Task<BranchDto> CreateAsync(CreateBranchRequest req, CancellationToken ct);
    Task<BranchDto> UpdateAsync(string branchId, UpdateBranchRequest req, CancellationToken ct);
    Task DeleteAsync(string branchId, CancellationToken ct);

    Task<BranchUserDto> CreateUserAsync(string branchId, CreateBranchUserRequest req, CancellationToken ct);
    Task<BranchUserDto> UpdateUserPinAsync(string branchId, string branchUserId, UpdateBranchUserPinRequest req, CancellationToken ct);
    Task DeleteUserAsync(string branchId, string branchUserId, CancellationToken ct);

    Task<BranchLoginResponse> LoginAsync(BranchLoginRequest req, string jwtSecret, CancellationToken ct);
}
