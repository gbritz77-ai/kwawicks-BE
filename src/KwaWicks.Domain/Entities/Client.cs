namespace KwaWicks.Domain.Entities;

public enum ClientType
{
    COD = 0,
    Credit = 1
}

public class Client
{
    public string ClientId { get; set; } = Guid.NewGuid().ToString("N");

    public string ClientName { get; set; } = string.Empty;
    public string ClientAddress { get; set; } = string.Empty;

    // keep it flexible: phone/email/notes etc
    public string ClientContactDetails { get; set; } = string.Empty;

    public ClientType ClientType { get; set; } = ClientType.COD;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}