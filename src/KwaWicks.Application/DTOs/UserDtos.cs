namespace KwaWicks.Application.DTOs;

public class UserDto
{
    public string Username { get; set; } = "";
    public string Group { get; set; } = "";
    public bool Enabled { get; set; }
    public string UserStatus { get; set; } = "";
}

public class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Pin { get; set; } = "";
    public string Group { get; set; } = "Driver";
}

public class SetPinRequest
{
    public string Pin { get; set; } = "";
}

public class UpdateGroupRequest
{
    public string NewGroup { get; set; } = "";
    public string OldGroup { get; set; } = "";
}
