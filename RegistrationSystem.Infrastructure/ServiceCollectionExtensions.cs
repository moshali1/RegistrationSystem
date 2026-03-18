using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using RegistrationSystem.Core.Application.Auditing;
using RegistrationSystem.Core.Application.Azure;
using RegistrationSystem.Core.Application.CompetitionRounds;
using RegistrationSystem.Core.Application.NiqabBypasses;
using RegistrationSystem.Core.Application.Scheduling;
using RegistrationSystem.Core.Application.Registrations;
using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Application.Users;
using RegistrationSystem.Core.Domain.CompetitionRounds;
using RegistrationSystem.Core.Domain.Consents;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Scheduling;
using RegistrationSystem.Core.Domain.Messaging;
using RegistrationSystem.Core.Domain.Users;
using RegistrationSystem.Core.Application.Messaging;
using RegistrationSystem.Infrastructure.Graph;
using RegistrationSystem.Infrastructure.Messaging;
using RegistrationSystem.Infrastructure.Mongo;
using RegistrationSystem.Infrastructure.Persistence;

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

        // Azure Storage
        var storageOptions = new AzureStorageOptions();
        configuration.GetSection(AzureStorageOptions.SectionName).Bind(storageOptions);
        services.AddSingleton(storageOptions);
        services.AddScoped<BlobStorageService>();
        services.AddScoped<BlobSasService>();

        // Azure Image Analysis (used for OCR and people detection)
        var imageAnalysisOptions = new AzureImageAnalysisOptions();
        configuration.GetSection(AzureImageAnalysisOptions.SectionName).Bind(imageAnalysisOptions);
        services.AddSingleton(imageAnalysisOptions);
        services.AddScoped<ImageAnalysisService>();

        // File Validation (orchestrates Azure services)
        services.AddScoped<FileValidationService>();

        // Azure OpenAI (used for AI-powered ID verification)
        var openAIOptions = new AzureOpenAIOptions();
        configuration.GetSection(AzureOpenAIOptions.SectionName).Bind(openAIOptions);
        services.AddSingleton(openAIOptions);
        services.AddScoped<OpenAI.Chat.ChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<AzureOpenAIOptions>();
            var azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(
                new Uri(opts.Endpoint),
                new Azure.AzureKeyCredential(opts.Key));
            return azureClient.GetChatClient(opts.DeploymentName);
        });
        services.AddScoped<IdVerificationService>();

        // Email (SendGrid)
        var emailOptions = new EmailOptions();
        configuration.GetSection(EmailOptions.SectionName).Bind(emailOptions);
        services.AddSingleton(emailOptions);
        services.AddScoped<IEmailService, SendGridEmailService>();

        // Repositories
        services.AddScoped<ICompetitionSettingsRepository, MongoCompetitionSettingsRepository>();
        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IConsentRepository, MongoConsentRepository>();
        services.AddScoped<IRegistrationRepository, MongoRegistrationRepository>();
        services.AddScoped<INiqabBypassRepository, MongoNiqabBypassRepository>();
        services.AddScoped<IAuditRepository, MongoAuditRepository>();
        services.AddScoped<ICompetitionProgressRepository, MongoCompetitionProgressRepository>();
        services.AddScoped<IEmailTemplateRepository, MongoEmailTemplateRepository>();
        services.AddScoped<ISchedulingBookingRepository, MongoSchedulingBookingRepository>();

        // Application Services
        services.AddScoped<RegistrationService>();
        services.AddScoped<NiqabBypassService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<CompetitionProgressService>();
        services.AddScoped<EmailTemplateService>();
        services.AddScoped<SchedulingService>();

        return services;
    }

    private static void RegisterMongoSerializers()
    {
        // These calls are safe to make once per app start
        BsonSerializer.RegisterSerializer(new DateOnlySerializer());

        // CompetitionProgress - Map ObjectId to string for Id property
        if (!BsonClassMap.IsClassMapRegistered(typeof(CompetitionProgress)))
        {
            BsonClassMap.RegisterClassMap<CompetitionProgress>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(c => c.Id)
                    .SetIdGenerator(MongoDB.Bson.Serialization.IdGenerators.StringObjectIdGenerator.Instance)
                    .SetSerializer(new StringSerializer(BsonType.ObjectId));
            });
        }

        // EmailTemplate - Map ObjectId to string for Id property
        if (!BsonClassMap.IsClassMapRegistered(typeof(EmailTemplate)))
        {
            BsonClassMap.RegisterClassMap<EmailTemplate>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(c => c.Id)
                    .SetIdGenerator(MongoDB.Bson.Serialization.IdGenerators.StringObjectIdGenerator.Instance)
                    .SetSerializer(new StringSerializer(BsonType.ObjectId));
            });
        }

        // SchedulingBooking - Map ObjectId to string for Id property
        if (!BsonClassMap.IsClassMapRegistered(typeof(SchedulingBooking)))
        {
            BsonClassMap.RegisterClassMap<SchedulingBooking>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(c => c.Id)
                    .SetIdGenerator(MongoDB.Bson.Serialization.IdGenerators.StringObjectIdGenerator.Instance)
                    .SetSerializer(new StringSerializer(BsonType.ObjectId));
            });
        }
    }
}