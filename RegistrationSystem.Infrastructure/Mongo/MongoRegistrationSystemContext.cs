using MongoDB.Driver;
using RegistrationSystem.Core.Domain.CompetitionRounds;
using RegistrationSystem.Core.Domain.Consents;
using RegistrationSystem.Core.Domain.NiqabBypasses;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Settings;
using RegistrationSystem.Core.Domain.Messaging;
using RegistrationSystem.Core.Domain.Users;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoRegistrationSystemContext
{
    public IMongoDatabase Database { get; }
    public IMongoCollection<CompetitionSettings> CompetitionSettings { get; }
    public IMongoCollection<User> Users { get; }
    public IMongoCollection<ConsentRecord> Consents { get; }
    public IMongoCollection<Registration> Registrations { get; }
    public IMongoCollection<NiqabBypass> NiqabBypasses { get; }
    public IMongoCollection<EmailTemplate> EmailTemplates { get; }
    public IMongoCollection<CompetitionProgress> CompetitionProgress { get; }

    public MongoRegistrationSystemContext(MongoClient client, MongoOptions options)
    {
        Database = client.GetDatabase(options.DatabaseName);

        CompetitionSettings = Database.GetCollection<CompetitionSettings>(
            options.CompetitionSettingsCollectionName);

        Users = Database.GetCollection<User>(
            options.UsersCollectionName);

        Consents = Database.GetCollection<ConsentRecord>(
            options.ConsentsCollectionName);

        Registrations = Database.GetCollection<Registration>(
            options.RegistrationsCollectionName);

        NiqabBypasses = Database.GetCollection<NiqabBypass>(
            options.NiqabBypassesCollectionName);

        EmailTemplates = Database.GetCollection<EmailTemplate>(
            options.EmailTemplatesCollectionName);

        CompetitionProgress = Database.GetCollection<CompetitionProgress>(
            options.CompetitionProgressCollectionName);
    }
}