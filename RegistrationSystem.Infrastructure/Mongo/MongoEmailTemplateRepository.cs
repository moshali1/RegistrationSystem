using MongoDB.Bson;
using MongoDB.Driver;
using RegistrationSystem.Core.Application.Messaging;
using RegistrationSystem.Core.Domain.Messaging;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoEmailTemplateRepository : IEmailTemplateRepository
{
    private readonly IMongoCollection<EmailTemplate> _collection;

    public MongoEmailTemplateRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.EmailTemplates;
    }

    public async Task<List<EmailTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(_ => true)
            .SortByDescending(t => t.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmailTemplate?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(t => t.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(template.Id))
        {
            template.Id = ObjectId.GenerateNewId().ToString();
            await _collection.InsertOneAsync(template, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(
                t => t.Id == template.Id,
                template,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(t => t.Id == id, cancellationToken);
    }
}
