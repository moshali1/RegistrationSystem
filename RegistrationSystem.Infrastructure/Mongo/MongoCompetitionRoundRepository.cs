using RegistrationSystem.Core.Application.CompetitionRounds;
using RegistrationSystem.Core.Domain.CompetitionRounds;
using RegistrationSystem.Infrastructure.Mongo;

namespace RegistrationSystem.Infrastructure.Persistence;

public class MongoCompetitionRoundRepository : ICompetitionRoundRepository
{
    private readonly IMongoCollection<CompetitionRound> _collection;

    public MongoCompetitionRoundRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.CompetitionRounds;
    }

    public async Task<CompetitionRound?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CompetitionRound?> GetByRegistrationIdAsync(
        string registrationId,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.RegistrationId == registrationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByRegistrationIdsAsync(
        IEnumerable<string> registrationIds,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompetitionRound>.Filter.In(r => r.RegistrationId, registrationIds);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByCompetitionYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CompetitionYear == competitionYear)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByVideoQualificationStatusAsync(
        VideoQualificationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CompetitionYear == competitionYear &&
                       r.VideoQualification == status)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetWithPendingAcknowledgmentsAsync(
    int competitionYear,
    CancellationToken cancellationToken = default)
    {
        var filter = Builders<CompetitionRound>.Filter.And(
            Builders<CompetitionRound>.Filter.Eq(r => r.CompetitionYear, competitionYear),
            Builders<CompetitionRound>.Filter.Or(
                Builders<CompetitionRound>.Filter.And(
                    Builders<CompetitionRound>.Filter.Ne(r => r.ScreeningRoundDateTime, null),
                    Builders<CompetitionRound>.Filter.Eq(r => r.ScreeningRoundBypass, false),
                    Builders<CompetitionRound>.Filter.Eq(r => r.ScreeningRoundAcknowledged, false)
                ),
                Builders<CompetitionRound>.Filter.And(
                    Builders<CompetitionRound>.Filter.Ne(r => r.PreliminaryRoundDateTime, null),
                    Builders<CompetitionRound>.Filter.Eq(r => r.PreliminaryRoundAcknowledged, false)
                ),
                Builders<CompetitionRound>.Filter.And(
                    Builders<CompetitionRound>.Filter.Ne(r => r.FinalRoundDateTime, null),
                    Builders<CompetitionRound>.Filter.Eq(r => r.FinalRoundAcknowledged, false)
                )
            )
        );

        return await _collection
            .Find(filter)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = new DateTimeOffset(roundDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endOfDay = new DateTimeOffset(roundDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var filter = Builders<CompetitionRound>.Filter.And(
            Builders<CompetitionRound>.Filter.Eq(r => r.CompetitionYear, competitionYear),
            Builders<CompetitionRound>.Filter.Gte(r => r.PreliminaryRoundDateTime, startOfDay),
            Builders<CompetitionRound>.Filter.Lte(r => r.PreliminaryRoundDateTime, endOfDay)
        );

        return await _collection
            .Find(filter)
            .SortBy(r => r.PreliminaryRoundDateTime)
            .ThenBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = new DateTimeOffset(roundDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endOfDay = new DateTimeOffset(roundDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var filter = Builders<CompetitionRound>.Filter.And(
            Builders<CompetitionRound>.Filter.Eq(r => r.CompetitionYear, competitionYear),
            Builders<CompetitionRound>.Filter.Gte(r => r.FinalRoundDateTime, startOfDay),
            Builders<CompetitionRound>.Filter.Lte(r => r.FinalRoundDateTime, endOfDay)
        );

        return await _collection
            .Find(filter)
            .SortBy(r => r.FinalRoundDateTime)
            .ThenBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CompetitionYear == competitionYear &&
                       r.PreliminaryRoundDateTime == roundDateTime)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CompetitionYear == competitionYear &&
                       r.FinalRoundDateTime == roundDateTime)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(CompetitionRound round, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(round.Id))
        {
            round.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            await _collection.InsertOneAsync(round, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(
                r => r.Id == round.Id,
                round,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken);
    }

    public async Task DeleteByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(r => r.RegistrationId == registrationId, cancellationToken);
    }

    public async Task<int> CountByVideoQualificationStatusAsync(
        VideoQualificationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return (int)await _collection
            .CountDocumentsAsync(
                r => r.CompetitionYear == competitionYear &&
                     r.VideoQualification == status,
                cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<CompetitionRound>> GetByCategoryAsync(
    string categoryId,
    int competitionYear,
    CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CategoryId == categoryId && r.CompetitionYear == competitionYear)
            .SortBy(r => r.CompetitorName)
            .ToListAsync(cancellationToken);
    }


}