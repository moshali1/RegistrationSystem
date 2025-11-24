using MongoDB.Driver;

using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoRegistrationSystemContext
{
    public IMongoDatabase Database { get; }
    public IMongoCollection<CompetitionSettings> CompetitionSettings { get; }

    public MongoRegistrationSystemContext(MongoClient client, MongoOptions options)
    {
        Database = client.GetDatabase(options.DatabaseName);
        CompetitionSettings = Database.GetCollection<CompetitionSettings>(
            options.CompetitionSettingsCollectionName);
    }
}
