namespace KwaWicks.Domain.Entities;

public class Branch
{
    public string BranchId    { get; set; } = Guid.NewGuid().ToString();
    public string Name        { get; set; } = "";
    public string Location    { get; set; } = "";
    public bool   IsActive    { get; set; } = true;
    public List<string> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BranchUser
{
    public string BranchUserId { get; set; } = Guid.NewGuid().ToString();
    public string BranchId     { get; set; } = "";
    public string Username     { get; set; } = "";
    public string PinHash      { get; set; } = "";
    public bool   IsActive     { get; set; } = true;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
}
