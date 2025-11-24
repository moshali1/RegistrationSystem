using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Infrastructure.Mongo;

namespace RegistrationSystem.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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

        // Repositories
        services.AddScoped<ICompetitionSettingsRepository, MongoCompetitionSettingsRepository>();

        return services;
    }
}
