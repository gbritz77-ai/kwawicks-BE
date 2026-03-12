namespace KwaWicks.Domain.Entities;

public enum ClientType
{
    CODCASH = 0,
    CODEFT = 1,
    Credit = 2,
}

public class Client
{
    public string ClientId { get; set; } = Guid.NewGuid().ToString("N");

    public string ClientName { get; set; } = string.Empty;
    public string ClientAddress { get; set; } = string.Empty;

    // keep it flexible: phone/email/notes etc
    public string ClientContactDetails { get; set; } = string.Empty;

    public ClientType ClientType { get; set; } = ClientType.CODCASH;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}