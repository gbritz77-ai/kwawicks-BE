using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IStaffMemberService
{
    Task<StaffMemberDto> CreateAsync(CreateStaffMemberRequest request, CancellationToken ct);
    Task<StaffMemberDto?> GetByIdAsync(string staffMemberId, CancellationToken ct);
    Task<List<StaffMemberDto>> ListAsync(CancellationToken ct);
    Task<StaffMemberDto?> UpdateAsync(string staffMemberId, UpdateStaffMemberRequest request, CancellationToken ct);
}
