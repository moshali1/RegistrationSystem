using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Application.Users;
using RegistrationSystem.Core.Domain.Consents;
using RegistrationSystem.Core.Domain.Users;
using RegistrationSystem.Infrastructure.Graph;
using RegistrationSystem.Infrastructure.Mongo;

namespace RegistrationSystem.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        RegisterMongoSerializers();

        // Bind Mongo options from configuration
        var mongoOptions = new MongoOptions();
        configuration.GetSection(MongoOptions.SectionName).Bind(mongoOptions);
        services.AddSingleton(mongoOptions);

        // Mongo client & context
        services.AddSingleton(sp =>
        {
            return new MongoClient(mongoOptions.ConnectionString);
        });
        services.AddSingleton<MongoRegistrationSystemContext>();

        // Microsoft Graph
        var graphOptions = new MicrosoftGraphOptions();
        configuration.GetSection(MicrosoftGraphOptions.SectionName).Bind(graphOptions);
        services.AddSingleton(graphOptions);
        services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();

        // Repositories
        services.AddScoped<ICompetitionSettingsRepository, MongoCompetitionSettingsRepository>();
        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IConsentRepository, MongoConsentRepository>();

        return services;
    }

    private static void RegisterMongoSerializers()
    {
        // These calls are safe to make once per app start
        BsonSerializer.RegisterSerializer(new DateOnlySerializer());
    }
}