using MongoDB.Driver;
using RegistrationSystem.Core.Application.CompetitionRounds;
using RegistrationSystem.Core.Domain.CompetitionRounds;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoCompetitionProgressRepository : ICompetitionProgressRepository
{
    private readonly IMongoCollection<CompetitionProgress> _collection;

    public MongoCompetitionProgressRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.CompetitionProgress;
    }

    public async Task<CompetitionProgress?> GetByIdAsync(
        string id, CancellationToken cancellationToken = default) =>
        await _collection.Find(r => r.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CompetitionProgress?> GetByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default) =>
        await _collection.Find(r => r.RegistrationId == registrationId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CompetitionProgress>> GetByRegistrationIdsAsync(
        IEnumerable<string> registrationIds, CancellationToken cancellationToken = default)
    {
        var ids = registrationIds.ToList();
        var filter = Builders<CompetitionProgress>.Filter.In(r => r.RegistrationId, ids);
        return await _collection.Find(filter)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionProgress>> GetByCompetitionYearAsync(
        int year, CancellationToken cancellationToken = default) =>
        await _collection.Find(r => r.CompetitionYear == year)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CompetitionProgress>> GetByCategoryAsync(
        string categoryId, int year, CancellationToken cancellationToken = default) =>
        await _collection.Find(r => r.CategoryId == categoryId && r.CompetitionYear == year)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);

    public async Task SaveAsync(
        CompetitionProgress progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(progress.Id))
        {
            progress.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            await _collection.InsertOneAsync(progress, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(
                r => r.Id == progress.Id,
                progress,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    public async Task DeleteAsync(
        string id, CancellationToken cancellationToken = default) =>
        await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken);

    public async Task DeleteByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default) =>
        await _collection.DeleteOneAsync(r => r.RegistrationId == registrationId, cancellationToken);
}
