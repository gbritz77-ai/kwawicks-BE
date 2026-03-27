namespace KwaWicks.Domain.Entities;

public class StaffMember
{
    public string StaffMemberId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Phone { get; set; } = ""; // WhatsApp number
    public string Department { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
