using MongoDB.Driver;
using RegistrationSystem.Core.Domain.Consents;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoConsentRepository : IConsentRepository
{
    private readonly IMongoCollection<ConsentRecord> _collection;

    public MongoConsentRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.Consents;
    }

    public async Task<ConsentRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(c => c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ConsentRecord?> GetByUserAndYearAsync(string userId, int competitionYear, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(c => c.UserId == userId && c.CompetitionYear == competitionYear)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConsentRecord>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(c => c.UserId == userId)
            .SortByDescending(c => c.ConsentedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ConsentRecord consent, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(consent, cancellationToken: cancellationToken);
    }

    public async Task<bool> HasConsentedAsync(string userId, int competitionYear, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(c => c.UserId == userId && c.CompetitionYear == competitionYear)
            .AnyAsync(cancellationToken);
    }
}