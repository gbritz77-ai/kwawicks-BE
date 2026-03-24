using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using KwaWicks.Application.DTOs;
using KwaWicks.Application.Interfaces;

namespace KwaWicks.Application.Services;

public class UserManagementService : IUserManagementService
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly string _userPoolId;
    private static readonly string[] KnownGroups = ["Owner", "Finance", "Admin", "HubStaff", "Procurement", "Driver"];

    public UserManagementService(IAmazonCognitoIdentityProvider cognito, string userPoolId)
    {
        _cognito = cognito;
        _userPoolId = userPoolId;
    }

    public async Task<List<UserDto>> ListUsersAsync(CancellationToken ct = default)
    {
        // Build username → group map by listing each known group
        var userGroupMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in KnownGroups)
        {
            try
            {
                var req = new ListUsersInGroupRequest { UserPoolId = _userPoolId, GroupName = group, Limit = 60 };
                do
                {
                    var resp = await _cognito.ListUsersInGroupAsync(req, ct);
                    foreach (var u in resp.Users)
                        userGroupMap.TryAdd(u.Username, group);
                    req.NextToken = resp.NextToken;
                } while (req.NextToken != null);
            }
            catch { /* group may not exist yet */ }
        }

        var listReq = new ListUsersRequest { UserPoolId = _userPoolId, Limit = 60 };
        var allUsers = new List<UserDto>();
        do
        {
            var resp = await _cognito.ListUsersAsync(listReq, ct);
            foreach (var u in resp.Users)
            {
                allUsers.Add(new UserDto
                {
                    Username = u.Username,
                    Group = userGroupMap.TryGetValue(u.Username, out var g) ? g : "",
                    Enabled = u.Enabled ?? false,
                    UserStatus = u.UserStatus?.Value ?? ""
                });
            }
            listReq.PaginationToken = resp.PaginationToken;
        } while (listReq.PaginationToken != null);

        return allUsers.OrderBy(u => u.Username).ToList();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        await _cognito.AdminCreateUserAsync(new AdminCreateUserRequest
        {
            UserPoolId = _userPoolId,
            Username = request.Username,
            TemporaryPassword = request.Pin,
            MessageAction = MessageActionType.SUPPRESS
        }, ct);

        // Set permanent so driver doesn't need to change password on first login
        await _cognito.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = request.Username,
            Password = request.Pin,
            Permanent = true
        }, ct);

        if (!string.IsNullOrEmpty(request.Group))
        {
            await EnsureGroupExistsAsync(request.Group, ct);
            await _cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
            {
                UserPoolId = _userPoolId,
                Username = request.Username,
                GroupName = request.Group
            }, ct);
        }

        return new UserDto { Username = request.Username, Group = request.Group, Enabled = true, UserStatus = "CONFIRMED" };
    }

    public async Task SetPinAsync(string username, string newPin, CancellationToken ct = default)
    {
        await _cognito.AdminSetUserPasswordAsync(new AdminSetUserPasswordRequest
        {
            UserPoolId = _userPoolId,
            Username = username,
            Password = newPin,
            Permanent = true
        }, ct);
    }

    public async Task UpdateGroupAsync(string username, string newGroup, string oldGroup, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(oldGroup) && oldGroup != newGroup)
        {
            try
            {
                await _cognito.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest
                {
                    UserPoolId = _userPoolId,
                    Username = username,
                    GroupName = oldGroup
                }, ct);
            }
            catch { /* ignore if not in group */ }
        }

        if (!string.IsNullOrEmpty(newGroup))
        {
            await EnsureGroupExistsAsync(newGroup, ct);
            await _cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
            {
                UserPoolId = _userPoolId,
                Username = username,
                GroupName = newGroup
            }, ct);
        }
    }

    public async Task DeleteUserAsync(string username, CancellationToken ct = default)
    {
        await _cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
        {
            UserPoolId = _userPoolId,
            Username = username
        }, ct);
    }

    private async Task EnsureGroupExistsAsync(string groupName, CancellationToken ct)
    {
        try
        {
            await _cognito.CreateGroupAsync(new Amazon.CognitoIdentityProvider.Model.CreateGroupRequest
            {
                UserPoolId = _userPoolId,
                GroupName = groupName
            }, ct);
        }
        catch (Amazon.CognitoIdentityProvider.Model.GroupExistsException) { /* already exists, fine */ }
    }
}
