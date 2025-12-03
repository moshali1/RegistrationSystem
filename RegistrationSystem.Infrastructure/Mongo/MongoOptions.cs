namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string CompetitionSettingsCollectionName { get; set; } = "competitionSettings";
    public string UsersCollectionName { get; set; } = "users";
    public string ConsentsCollectionName { get; set; } = "consents";
}