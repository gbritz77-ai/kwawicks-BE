using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;
using KwaWicks.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace KwaWicks.Application.Services;

public class BranchService : IBranchService
{
    private readonly IBranchRepository _repo;

    public BranchService(IBranchRepository repo) => _repo = repo;

    public async Task<List<BranchDto>> ListAsync(CancellationToken ct)
    {
        var branches = await _repo.ListAsync(ct);
        var result = new List<BranchDto>();
        foreach (var b in branches)
        {
            var users = await _repo.ListUsersAsync(b.BranchId, ct);
            result.Add(MapDto(b, users));
        }
        return result;
    }

    public async Task<BranchDto> GetAsync(string branchId, CancellationToken ct)
    {
        var b = await _repo.GetAsync(branchId, ct)
            ?? throw new KeyNotFoundException($"Branch {branchId} not found.");
        var users = await _repo.ListUsersAsync(branchId, ct);
        return MapDto(b, users);
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ArgumentException("Branch name is required.");
        var branch = new Branch
        {
            Name        = req.Name.Trim(),
            Location    = req.Location?.Trim() ?? "",
            Permissions = req.Permissions ?? new()
        };
        await _repo.CreateAsync(branch, ct);
        return MapDto(branch, new());
    }

    public async Task<BranchDto> UpdateAsync(string branchId, UpdateBranchRequest req, CancellationToken ct)
    {
        var branch = await _repo.GetAsync(branchId, ct)
            ?? throw new KeyNotFoundException($"Branch {branchId} not found.");
        branch.Name        = req.Name.Trim();
        branch.Location    = req.Location?.Trim() ?? "";
        branch.IsActive    = req.IsActive;
        branch.Permissions = req.Permissions ?? new();
        await _repo.UpdateAsync(branch, ct);
        var users = await _repo.ListUsersAsync(branchId, ct);
        return MapDto(branch, users);
    }

    public async Task DeleteAsync(string branchId, CancellationToken ct)
    {
        var users = await _repo.ListUsersAsync(branchId, ct);
        foreach (var u in users)
            await _repo.DeleteUserAsync(branchId, u.BranchUserId, ct);
        await _repo.DeleteAsync(branchId, ct);
    }

    public async Task<BranchUserDto> CreateUserAsync(string branchId, CreateBranchUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(req.Pin) || req.Pin.Length < 4)
            throw new ArgumentException("PIN must be at least 4 digits.");

        var existing = await _repo.GetUserByUsernameAsync(req.Username.Trim(), ct);
        if (existing != null)
            throw new ArgumentException($"Username '{req.Username}' is already taken.");

        var user = new BranchUser
        {
            BranchId = branchId,
            Username = req.Username.Trim(),
            PinHash  = HashPin(req.Pin),
        };
        await _repo.CreateUserAsync(user, ct);
        return MapUserDto(user);
    }

    public async Task<BranchUserDto> UpdateUserPinAsync(string branchId, string branchUserId, UpdateBranchUserPinRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Pin) || req.Pin.Length < 4)
            throw new ArgumentException("PIN must be at least 4 digits.");
        var users = await _repo.ListUsersAsync(branchId, ct);
        var user = users.FirstOrDefault(u => u.BranchUserId == branchUserId)
            ?? throw new KeyNotFoundException("Branch user not found.");
        user.PinHash = HashPin(req.Pin);
        await _repo.UpdateUserAsync(user, ct);
        return MapUserDto(user);
    }

    public async Task DeleteUserAsync(string branchId, string branchUserId, CancellationToken ct)
    {
        await _repo.DeleteUserAsync(branchId, branchUserId, ct);
    }

    public async Task<BranchLoginResponse> LoginAsync(BranchLoginRequest req, string jwtSecret, CancellationToken ct)
    {
        var user = await _repo.GetUserByUsernameAsync(req.Username.Trim(), ct)
            ?? throw new UnauthorizedAccessException("Invalid username or PIN.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("This user account is disabled.");

        if (!VerifyPin(req.Pin, user.PinHash))
            throw new UnauthorizedAccessException("Invalid username or PIN.");

        var branch = await _repo.GetAsync(user.BranchId, ct)
            ?? throw new UnauthorizedAccessException("Branch not found or inactive.");

        if (!branch.IsActive)
            throw new UnauthorizedAccessException("This branch is disabled.");

        const int expiresIn = 86400; // 24 hours
        var token = IssueJwt(user, branch, jwtSecret, expiresIn);

        return new BranchLoginResponse
        {
            Token       = token,
            BranchId    = branch.BranchId,
            BranchName  = branch.Name,
            Username    = user.Username,
            Permissions = branch.Permissions,
            ExpiresIn   = expiresIn
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string HashPin(string pin)
    {
        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);
        var saltB64 = Convert.ToBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin),
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);
        return $"{saltB64}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPin(string pin, string storedHash)
    {
        try
        {
            var parts = storedHash.Split(':', 2);
            var salt  = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual   = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(pin), salt, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }

    private static string IssueJwt(BranchUser user, Branch branch, string secret, int expiresIn)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims  = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.BranchUserId),
            new("hub_id",  branch.BranchId),
            new("hub_name", branch.Name),
            new("username", user.Username),
            new(ClaimTypes.Role, "BranchUser"),
        };
        foreach (var p in branch.Permissions)
            claims.Add(new Claim("hub_permission", p));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(expiresIn),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static BranchDto MapDto(Branch b, List<BranchUser> users) => new()
    {
        BranchId    = b.BranchId,
        Name        = b.Name,
        Location    = b.Location,
        IsActive    = b.IsActive,
        Permissions = b.Permissions,
        CreatedAt   = b.CreatedAt.ToString("yyyy-MM-dd"),
        Users       = users.Select(MapUserDto).ToList(),
    };

    private static BranchUserDto MapUserDto(BranchUser u) => new()
    {
        BranchUserId = u.BranchUserId,
        BranchId     = u.BranchId,
        Username     = u.Username,
        IsActive     = u.IsActive,
    };
}
