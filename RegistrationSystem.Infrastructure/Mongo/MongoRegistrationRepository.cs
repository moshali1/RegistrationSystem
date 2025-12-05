using MongoDB.Driver;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Infrastructure.Mongo;

namespace RegistrationSystem.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of IRegistrationRepository.
/// </summary>
public class MongoRegistrationRepository : IRegistrationRepository
{
    private readonly IMongoCollection<Registration> _collection;

    public MongoRegistrationRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.Registrations;
    }

    public async Task<Registration?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCreatorUserIdAsync(
        string creatorUserId,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CreatorUserId == creatorUserId)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCompetitionYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CompetitionYear == competitionYear)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCreatorAndYearAsync(
        string creatorUserId,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CreatorUserId == creatorUserId && r.CompetitionYear == competitionYear)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> FindDuplicatesAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        // Case-insensitive comparison for names
        var firstNameLower = firstName.ToLowerInvariant();
        var lastNameLower = lastName.ToLowerInvariant();

        var filter = Builders<Registration>.Filter.And(
            Builders<Registration>.Filter.Eq(r => r.CompetitionYear, competitionYear),
            Builders<Registration>.Filter.Eq(r => r.PersonalInfo.DateOfBirth, dateOfBirth),
            Builders<Registration>.Filter.Where(r =>
                r.PersonalInfo.FirstName.ToLowerInvariant() == firstNameLower &&
                r.PersonalInfo.LastName.ToLowerInvariant() == lastNameLower)
        );

        return await _collection
            .Find(filter)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCreatorDivisionAndYearAsync(
        string creatorUserId,
        string divisionId,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.CreatorUserId == creatorUserId &&
                       r.CompetitionSelection.DivisionId == divisionId &&
                       r.CompetitionYear == competitionYear &&
                       r.Status != RegistrationStatus.Withdrawn &&
                       r.Status != RegistrationStatus.Disqualified)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByStatusAsync(
        RegistrationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(r => r.Status == status && r.CompetitionYear == competitionYear)
            .SortByDescending(r => r.SubmittedAt ?? r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Registration registration, CancellationToken cancellationToken = default)
    {
        registration.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrEmpty(registration.Id))
        {
            registration.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            registration.CreatedAt = DateTimeOffset.UtcNow;
            await _collection.InsertOneAsync(registration, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(
                r => r.Id == registration.Id,
                registration,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<int> CountByStatusAsync(
        RegistrationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return (int)await _collection
            .CountDocumentsAsync(
                r => r.Status == status && r.CompetitionYear == competitionYear,
                cancellationToken: cancellationToken);
    }

    public async Task<int> CountByYearAsync(int competitionYear, CancellationToken cancellationToken = default)
    {
        return (int)await _collection
            .CountDocumentsAsync(
                r => r.CompetitionYear == competitionYear,
                cancellationToken: cancellationToken);
    }

    public async Task<int> GetMaxCidSequenceAsync(
        int competitionYear,
        string cidPrefix,
        CancellationToken cancellationToken = default)
    {
        // Find all CIDs that start with the given prefix for this year
        // CID format: [Prefix][3-digit sequence] e.g., "M3001"
        var filter = Builders<Registration>.Filter.And(
            Builders<Registration>.Filter.Eq(r => r.CompetitionYear, competitionYear),
            Builders<Registration>.Filter.Regex(r => r.Cid, new MongoDB.Bson.BsonRegularExpression($"^{cidPrefix}\\d{{3}}$"))
        );

        var registrations = await _collection
            .Find(filter)
            .Project(r => r.Cid)
            .ToListAsync(cancellationToken);

        if (!registrations.Any())
            return 0;

        // Extract sequence numbers and find the max
        var maxSequence = registrations
            .Where(cid => !string.IsNullOrEmpty(cid))
            .Select(cid =>
            {
                // Extract the last 3 characters (sequence number)
                var sequencePart = cid!.Substring(cidPrefix.Length);
                return int.TryParse(sequencePart, out var seq) ? seq : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return maxSequence;
    }
}