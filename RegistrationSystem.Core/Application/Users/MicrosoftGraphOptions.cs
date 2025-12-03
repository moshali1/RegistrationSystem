using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using RegistrationSystem.Core.Application.Users;

namespace RegistrationSystem.Infrastructure.Graph;

public class MicrosoftGraphOptions
{
    public const string SectionName = "MicrosoftGraph";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public class MicrosoftGraphService : IMicrosoftGraphService
{
    private readonly GraphServiceClient _graphClient;
    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];

    public MicrosoftGraphService(MicrosoftGraphOptions options)
    {
        var clientSecretCredential = new ClientSecretCredential(
            options.TenantId,
            options.ClientId,
            options.ClientSecret);

        _graphClient = new GraphServiceClient(clientSecretCredential, Scopes);
    }

    public async Task<GraphUserProfile> GetUserProfileAsync(
        string objectIdentifier,
        CancellationToken cancellationToken = default)
    {
        var user = await _graphClient.Users[objectIdentifier]
            .GetAsync(requestConfig =>
            {
                requestConfig.QueryParameters.Select = ["displayName", "givenName", "surname"];
            }, cancellationToken);

        return new GraphUserProfile
        {
            DisplayName = user?.DisplayName ?? string.Empty,
            GivenName = user?.GivenName ?? string.Empty,
            Surname = user?.Surname ?? string.Empty
        };
    }

    public async Task UpdateUserProfileAsync(
        string objectIdentifier,
        string displayName,
        string givenName,
        string surname,
        CancellationToken cancellationToken = default)
    {
        var userUpdate = new User
        {
            DisplayName = displayName,
            GivenName = givenName,
            Surname = surname
        };

        await _graphClient.Users[objectIdentifier]
            .PatchAsync(userUpdate, cancellationToken: cancellationToken);
    }
}