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

    public async Task<CompetitionSettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompetitionSettings>.Filter.Eq(s => s.Id, CompetitionSettings.SingletonId);
        var settings = await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new CompetitionSettings();
            await _collection.InsertOneAsync(settings, cancellationToken: cancellationToken);
        }

        return settings;
    }

    public async Task SaveAsync(
        CompetitionSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Id = CompetitionSettings.SingletonId; // Always enforce singleton ID

        var filter = Builders<CompetitionSettings>.Filter.Eq(s => s.Id, CompetitionSettings.SingletonId);
        var options = new ReplaceOptions { IsUpsert = true };

        await _collection.ReplaceOneAsync(filter, settings, options, cancellationToken);
    }
}
