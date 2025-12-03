namespace RegistrationSystem.Core.Application.Users;

/// <summary>
/// Service for interacting with Microsoft Graph API.
/// </summary>
public interface IMicrosoftGraphService
{
    /// <summary>
    /// Gets user profile from Azure AD/Entra.
    /// </summary>
    Task<GraphUserProfile> GetUserProfileAsync(
        string objectIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user profile in Azure AD/Entra.
    /// </summary>
    Task UpdateUserProfileAsync(
        string objectIdentifier,
        string displayName,
        string givenName,
        string surname,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// User profile data from Microsoft Graph.
/// </summary>
public class GraphUserProfile
{
    public string DisplayName { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
}