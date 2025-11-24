namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoOptions
{
    public const string SectionName = "Mongo"; // for appsettings binding

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string CompetitionSettingsCollectionName { get; set; } = "competitionSettings";
}
