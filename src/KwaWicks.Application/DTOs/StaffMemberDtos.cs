namespace KwaWicks.Application.DTOs;

public class CreateStaffMemberRequest
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Department { get; set; } = "";
}

public class UpdateStaffMemberRequest
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Department { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class StaffMemberDto
{
    public string StaffMemberId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Department { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
