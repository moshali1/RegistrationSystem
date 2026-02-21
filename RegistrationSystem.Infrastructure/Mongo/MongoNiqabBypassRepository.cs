using MongoDB.Driver;
using RegistrationSystem.Core.Application.NiqabBypasses;
using RegistrationSystem.Core.Domain.NiqabBypasses;
using RegistrationSystem.Infrastructure.Mongo;

namespace RegistrationSystem.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of INiqabBypassRepository.
/// </summary>
public class MongoNiqabBypassRepository : INiqabBypassRepository
{
    private readonly IMongoCollection<NiqabBypass> _collection;

    public MongoNiqabBypassRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.NiqabBypasses;

        // Create indexes
        var indexKeys = Builders<NiqabBypass>.IndexKeys;

        // Unique index on code
        _collection.Indexes.CreateOne(new CreateIndexModel<NiqabBypass>(
            indexKeys.Ascending(x => x.Code),
            new CreateIndexOptions { Unique = true }));

        // Index for finding by competitor
        _collection.Indexes.CreateOne(new CreateIndexModel<NiqabBypass>(
            indexKeys.Combine(
                indexKeys.Ascending(x => x.FirstName),
                indexKeys.Ascending(x => x.LastName),
                indexKeys.Ascending(x => x.DateOfBirth),
                indexKeys.Ascending(x => x.CompetitionYear))));
    }

    public async Task<NiqabBypass?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(x => x.Code == code.ToUpperInvariant())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NiqabBypass>> GetByYearAsync(int competitionYear, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(x => x.CompetitionYear == competitionYear)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<NiqabBypass?> FindUnusedBypassAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        // MongoDB doesn't have case-insensitive comparison built-in for this pattern,
        // so we'll do a case-sensitive search and filter in memory
        var candidates = await _collection
            .Find(x => x.CompetitionYear == competitionYear &&
                       x.DateOfBirth == dateOfBirth &&
                       !x.IsUsed)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(x =>
            x.FirstName.Trim().Equals(firstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            x.LastName.Trim().Equals(lastName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<NiqabBypass?> FindClaimedBypassAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        // Find a bypass that has been claimed (IsUsed = true)
        var candidates = await _collection
            .Find(x => x.CompetitionYear == competitionYear &&
                       x.DateOfBirth == dateOfBirth &&
                       x.IsUsed)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(x =>
            x.FirstName.Trim().Equals(firstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            x.LastName.Trim().Equals(lastName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(NiqabBypass bypass, CancellationToken cancellationToken = default)
    {
        // Ensure code is uppercase
        bypass.Code = bypass.Code.ToUpperInvariant();

        var filter = Builders<NiqabBypass>.Filter.Eq(x => x.Id, bypass.Id);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, bypass, options, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<NiqabBypass?> FindByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(x => x.RegistrationId == registrationId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
