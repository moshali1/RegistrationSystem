using RegistrationSystem.Core.Domain.Users;
using System.Security.Claims;

namespace RegistrationSystem.Core.Application.Users;

public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IMicrosoftGraphService _graphService;

    // Claim types from Azure Entra External ID
    private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string EmailClaimType = "emails"; // Entra External ID uses "emails" (plural)
    private const string EmailClaimTypeFallback = ClaimTypes.Email;
    private const string PreferredUsernameClaimType = "preferred_username"; // Often contains email
    private const string RoleClaimType = ClaimTypes.Role;
    private const string UserTypeClaimType = "user_type"; // Custom claim - adjust if different

    public UserService(IUserRepository repository, IMicrosoftGraphService graphService)
    {
        _repository = repository;
        _graphService = graphService;
    }

    /// <summary>
    /// Synchronizes user from claims principal and Microsoft Graph.
    /// Creates new user or updates existing.
    /// Claims provide: Email, Role, UserType
    /// Graph API provides: DisplayName, FirstName, LastName
    /// Called on token validation (login).
    /// </summary>
    public async Task<User> SyncFromClaimsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var objectId = GetRequiredClaim(principal, ObjectIdentifierClaimType);

        var user = await _repository.GetByObjectIdentifierAsync(objectId, cancellationToken);
        var isNewUser = user is null;

        user ??= new User { ObjectIdentifier = objectId };

        // Get values from claims (Email, Role, UserType)
        var claimEmail = GetEmail(principal);
        var claimRole = GetRole(principal);
        var claimUserType = GetUserType(principal);

        // Get values from Graph API (DisplayName, FirstName, LastName)
        var graphProfile = await _graphService.GetUserProfileAsync(objectId, cancellationToken);

        // Check if anything changed
        var hasChanges = isNewUser ||
            user.Email != claimEmail ||
            user.Role != claimRole ||
            user.UserType != claimUserType ||
            user.DisplayName != graphProfile.DisplayName ||
            user.FirstName != graphProfile.GivenName ||
            user.LastName != graphProfile.Surname;

        if (hasChanges)
        {
            user.Email = claimEmail;
            user.Role = claimRole;
            user.UserType = claimUserType;
            user.DisplayName = graphProfile.DisplayName;
            user.FirstName = graphProfile.GivenName;
            user.LastName = graphProfile.Surname;

            await _repository.SaveAsync(user, cancellationToken);
        }

        return user;
    }

    /// <summary>
    /// Gets user by ID.
    /// </summary>
    public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    /// <summary>
    /// Gets user by Object Identifier (Azure AD oid).
    /// </summary>
    public Task<User?> GetByObjectIdentifierAsync(string objectIdentifier, CancellationToken cancellationToken = default)
        => _repository.GetByObjectIdentifierAsync(objectIdentifier, cancellationToken);

    /// <summary>
    /// Updates user profile (editable fields only).
    /// Updates both local database AND Azure AD.
    /// Only allowed for admin users.
    /// </summary>
    public async Task UpdateProfileAsync(
        string userId,
        string firstName,
        string lastName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"User not found: {userId}");

        var trimmedFirstName = firstName.Trim();
        var trimmedLastName = lastName.Trim();
        var trimmedDisplayName = displayName.Trim();

        // Update Azure AD first (if it fails, don't update local)
        await _graphService.UpdateUserProfileAsync(
            user.ObjectIdentifier,
            trimmedDisplayName,
            trimmedFirstName,
            trimmedLastName,
            cancellationToken);

        // Update local database
        user.FirstName = trimmedFirstName;
        user.LastName = trimmedLastName;
        user.DisplayName = trimmedDisplayName;

        await _repository.SaveAsync(user, cancellationToken);
    }

    /// <summary>
    /// Completes one-time profile verification.
    /// Validates and formats names, updates Azure and database.
    /// Once completed, user cannot edit their profile again.
    /// </summary>
    public async Task CompleteProfileVerificationAsync(
        string objectIdentifier,
        string firstName,
        string lastName,
        string userType,
        CancellationToken cancellationToken = default)
    {
        // Look up by ObjectIdentifier (Azure OID), not MongoDB _id
        var user = await _repository.GetByObjectIdentifierAsync(objectIdentifier, cancellationToken)
            ?? throw new InvalidOperationException($"User not found: {objectIdentifier}");

        if (user.IsProfileVerified)
        {
            throw new InvalidOperationException("Profile has already been verified and cannot be changed.");
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        if (string.IsNullOrWhiteSpace(userType))
            throw new ArgumentException("User type is required.", nameof(userType));

        // Validate userType is one of allowed values
        var validUserTypes = new[] { "Student", "Parent", "Teacher" };
        if (!validUserTypes.Contains(userType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid user type. Must be Student, Parent, or Teacher.", nameof(userType));

        // Format names: trim, capitalize first letter of each word
        var formattedFirstName = FormatName(firstName);
        var formattedLastName = FormatName(lastName);
        var formattedDisplayName = $"{formattedFirstName} {formattedLastName}";
        var formattedUserType = char.ToUpper(userType[0]) + userType.Substring(1).ToLower();

        // Update Azure AD first (if it fails, don't update local)
        await _graphService.UpdateUserProfileAsync(
            objectIdentifier,
            formattedDisplayName,
            formattedFirstName,
            formattedLastName,
            cancellationToken);

        // Update local database
        user.FirstName = formattedFirstName;
        user.LastName = formattedLastName;
        user.DisplayName = formattedDisplayName;
        user.UserType = formattedUserType;
        user.IsProfileVerified = true;

        await _repository.SaveAsync(user, cancellationToken);
    }

    /// <summary>
    /// Formats a name by trimming whitespace and capitalizing the first letter of each word.
    /// </summary>
    private static string FormatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var formattedWords = words.Select(word =>
        {
            if (word.Length == 1)
                return char.ToUpper(word[0]).ToString();

            return char.ToUpper(word[0]) + word.Substring(1).ToLower();
        });

        return string.Join(" ", formattedWords);
    }

    #region Private Helpers

    private static string GetRequiredClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = GetClaimValue(principal, claimType);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required claim '{claimType}' not found.");
        }
        return value;
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirst(claimType)?.Value;
    }

    private static string GetEmail(ClaimsPrincipal principal)
    {
        // Entra External ID may use different claims for email
        var email = GetClaimValue(principal, EmailClaimType)
            ?? GetClaimValue(principal, EmailClaimTypeFallback)
            ?? GetClaimValue(principal, PreferredUsernameClaimType)
            ?? string.Empty;

        return email;
    }

    private static string GetRole(ClaimsPrincipal principal)
    {
        // Role comes from Azure - could be "Admin" or empty
        return GetClaimValue(principal, RoleClaimType) ?? string.Empty;
    }

    private static string GetUserType(ClaimsPrincipal principal)
    {
        // UserType is now a string - Teacher, Parent, Student, or empty
        return GetClaimValue(principal, UserTypeClaimType) ?? string.Empty;
    }

    #endregion
}