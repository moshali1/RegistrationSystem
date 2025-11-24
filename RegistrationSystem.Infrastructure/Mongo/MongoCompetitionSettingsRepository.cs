using MongoDB.Driver;

using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoCompetitionSettingsRepository : ICompetitionSettingsRepository
{
    private readonly IMongoCollection<CompetitionSettings> _collection;

    public MongoCompetitionSettingsRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.CompetitionSettings;
    }

    // There should be exactly one settings document identified by this Id
    private const string DefaultId = "default-competition-settings";

    public async Task<CompetitionSettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompetitionSettings>.Filter.Eq(s => s.Id, DefaultId);

        var settings = await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            // First-time setup: create a default settings document
            settings = new CompetitionSettings
            {
                Id = DefaultId
                // other defaults already come from your domain class
            };

            await _collection.InsertOneAsync(settings, cancellationToken: cancellationToken);
        }

        return settings;
    }

    public async Task SaveAsync(
        CompetitionSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Id))
        {
            settings.Id = DefaultId;
        }

        var filter = Builders<CompetitionSettings>.Filter.Eq(s => s.Id, settings.Id);

        var options = new ReplaceOptions { IsUpsert = true };

        await _collection.ReplaceOneAsync(
            filter,
            settings,
            options,
            cancellationToken);
    }
}
