using KwaWicks.Application.DTOs;

namespace KwaWicks.Application.Interfaces;

public interface IUserManagementService
{
    Task<List<UserDto>> ListUsersAsync(CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task SetPinAsync(string username, string newPin, CancellationToken ct = default);
    Task UpdateGroupAsync(string username, string newGroup, string oldGroup, CancellationToken ct = default);
    Task DeleteUserAsync(string username, CancellationToken ct = default);
}
