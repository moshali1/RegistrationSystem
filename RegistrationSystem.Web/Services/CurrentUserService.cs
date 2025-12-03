using Microsoft.AspNetCore.Components.Authorization;
using RegistrationSystem.Core.Application.Users;
using RegistrationSystem.Core.Domain.Users;

namespace RegistrationSystem.Web.Services;

/// <summary>
/// Provides access to the current authenticated user.
/// Automatically syncs user from claims on first access.
/// </summary>
public class CurrentUserService
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly UserService _userService;
    private User? _currentUser;
    private bool _initialized;

    public CurrentUserService(
        AuthenticationStateProvider authStateProvider,
        UserService userService)
    {
        _authStateProvider = authStateProvider;
        _userService = userService;
    }

    /// <summary>
    /// Gets the current authenticated user from the database.
    /// Returns null if not authenticated.
    /// Note: Does NOT sync from claims - that happens on login via OnTokenValidated.
    /// </summary>
    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return _currentUser;
        }

        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            _initialized = true;
            _currentUser = null;
            return null;
        }

        var objectId = principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        if (string.IsNullOrEmpty(objectId))
        {
            _initialized = true;
            _currentUser = null;
            return null;
        }

        _currentUser = await _userService.GetByObjectIdentifierAsync(objectId, cancellationToken);
        _initialized = true;
        return _currentUser;
    }

    /// <summary>
    /// Gets the current user directly from database, bypassing cache.
    /// Use this after profile updates to get fresh data.
    /// </summary>
    public async Task<User?> RefreshCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        _initialized = false;
        _currentUser = null;
        return await GetCurrentUserAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the current user's ID, or null if not authenticated.
    /// </summary>
    public async Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return user?.Id;
    }

    /// <summary>
    /// Checks if current user has Admin role.
    /// </summary>
    public async Task<bool> IsAdminAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return user?.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Forces a refresh of the current user from claims.
    /// Call after profile updates.
    /// </summary>
    public void Invalidate()
    {
        _initialized = false;
        _currentUser = null;
    }
}