using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;

namespace KwaWicks.Application.Services;

public class StaffMemberService : IStaffMemberService
{
    private readonly IStaffMemberRepository _repo;

    public StaffMemberService(IStaffMemberRepository repo)
    {
        _repo = repo;
    }

    public async Task<StaffMemberDto> CreateAsync(CreateStaffMemberRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");

        var member = new StaffMember
        {
            Name = request.Name.Trim(),
            Phone = request.Phone?.Trim() ?? "",
            Department = request.Department?.Trim() ?? "",
            IsActive = true
        };

        await _repo.CreateAsync(member, ct);
        return Map(member);
    }

    public async Task<StaffMemberDto?> GetByIdAsync(string staffMemberId, CancellationToken ct)
    {
        var m = await _repo.GetAsync(staffMemberId, ct);
        return m is null ? null : Map(m);
    }

    public async Task<List<StaffMemberDto>> ListAsync(CancellationToken ct)
    {
        var list = await _repo.ListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<StaffMemberDto?> UpdateAsync(string staffMemberId, UpdateStaffMemberRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetAsync(staffMemberId, ct);
        if (existing is null) return null;

        existing.Name = string.IsNullOrWhiteSpace(request.Name) ? existing.Name : request.Name.Trim();
        existing.Phone = request.Phone?.Trim() ?? existing.Phone;
        existing.Department = request.Department?.Trim() ?? existing.Department;
        existing.IsActive = request.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _repo.UpdateAsync(existing, ct);
        return Map(existing);
    }

    private static StaffMemberDto Map(StaffMember m) => new()
    {
        StaffMemberId = m.StaffMemberId,
        Name = m.Name,
        Phone = m.Phone,
        Department = m.Department,
        IsActive = m.IsActive,
        CreatedAtUtc = m.CreatedAtUtc,
        UpdatedAtUtc = m.UpdatedAtUtc
    };
}
