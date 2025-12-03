using MongoDB.Driver;
using RegistrationSystem.Core.Domain.Consents;
using RegistrationSystem.Core.Domain.Settings;
using RegistrationSystem.Core.Domain.Users;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoRegistrationSystemContext
{
    public IMongoDatabase Database { get; }
    public IMongoCollection<CompetitionSettings> CompetitionSettings { get; }
    public IMongoCollection<User> Users { get; }
    public IMongoCollection<ConsentRecord> Consents { get; }

    public MongoRegistrationSystemContext(MongoClient client, MongoOptions options)
    {
        Database = client.GetDatabase(options.DatabaseName);

        CompetitionSettings = Database.GetCollection<CompetitionSettings>(
            options.CompetitionSettingsCollectionName);

        Users = Database.GetCollection<User>(
            options.UsersCollectionName);

        Consents = Database.GetCollection<ConsentRecord>(
            options.ConsentsCollectionName);
    }
}