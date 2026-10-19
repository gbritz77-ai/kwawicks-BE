namespace KwaWicks.Application.DTOs;

public record CreateBranchRequest(string Name, string Location, List<string> Permissions);
public record UpdateBranchRequest(string Name, string Location, bool IsActive, List<string> Permissions);
public record CreateBranchUserRequest(string Username, string Pin);
public record UpdateBranchUserPinRequest(string Pin);
public record BranchLoginRequest(string Username, string Pin);

public class BranchDto
{
    public string BranchId          { get; set; } = "";
    public string Name              { get; set; } = "";
    public string Location          { get; set; } = "";
    public bool   IsActive          { get; set; } = true;
    public List<string> Permissions { get; set; } = new();
    public string CreatedAt         { get; set; } = "";
    public List<BranchUserDto> Users { get; set; } = new();
}

public class BranchUserDto
{
    public string BranchUserId { get; set; } = "";
    public string BranchId     { get; set; } = "";
    public string Username     { get; set; } = "";
    public bool   IsActive     { get; set; } = true;
}

public class BranchLoginResponse
{
    public string Token             { get; set; } = "";
    public string BranchId         { get; set; } = "";
    public string BranchName       { get; set; } = "";
    public string Username         { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
    public int ExpiresIn            { get; set; } = 86400;
}
