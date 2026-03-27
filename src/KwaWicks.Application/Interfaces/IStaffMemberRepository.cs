using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Interfaces;

public interface IStaffMemberRepository
{
    Task<StaffMember> CreateAsync(StaffMember member, CancellationToken ct);
    Task<StaffMember?> GetAsync(string staffMemberId, CancellationToken ct);
    Task<StaffMember> UpdateAsync(StaffMember member, CancellationToken ct);
    Task<List<StaffMember>> ListAsync(CancellationToken ct);
}
