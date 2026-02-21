using MongoDB.Bson;
using MongoDB.Driver;
using RegistrationSystem.Core.Application.Registrations;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Infrastructure.Mongo;

namespace RegistrationSystem.Infrastructure.Persistence;

public class MongoRegistrationRepository : IRegistrationRepository
{
    private readonly IMongoCollection<Registration> _collection;
    private readonly IMongoCollection<BsonDocument> _counters;

    public MongoRegistrationRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.Registrations;
        _counters = context.Database.GetCollection<BsonDocument>("counters");
    }

    public async Task<Registration?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCreatorUserIdAsync(
        string creatorUserId,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.CreatorUserId == creatorUserId)
            .SortByDescending(r => r.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCompetitionYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.CompetitionYear == competitionYear)
            .SortByDescending(r => r.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCreatorAndYearAsync(
        string creatorUserId,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.CreatorUserId == creatorUserId && r.CompetitionYear == competitionYear)
            .SortByDescending(r => r.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> FindDuplicatesAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        var firstNameLower = firstName.ToLowerInvariant();
        var lastNameLower = lastName.ToLowerInvariant();

        var filter = Builders<Registration>.Filter.And(
            Builders<Registration>.Filter.Eq(r => r.CompetitionYear, competitionYear),
            Builders<Registration>.Filter.Eq(r => r.PersonalInfo.DateOfBirth, dateOfBirth),
            Builders<Registration>.Filter.Where(r =>
                r.PersonalInfo.FirstName.ToLowerInvariant() == firstNameLower &&
                r.PersonalInfo.LastName.ToLowerInvariant() == lastNameLower)
        );

        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByCreatorDivisionAndYearAsync(
        string creatorUserId,
        string divisionId,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.CreatorUserId == creatorUserId &&
            r.CompetitionSelection.DivisionId == divisionId &&
            r.CompetitionYear == competitionYear &&
            r.Status != RegistrationStatus.Withdrawn &&
            r.Status != RegistrationStatus.Disqualified).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetByStatusAsync(
        RegistrationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(r => r.Status == status && r.CompetitionYear == competitionYear)
            .SortByDescending(r => r.Id).ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(registration.Id))
        {
            registration.Id = ObjectId.GenerateNewId().ToString();
            await _collection.InsertOneAsync(registration, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(r => r.Id == registration.Id, registration,
                new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
    }

    /// <summary>
    /// Partial update: only writes Status, StatusComment, and WithdrawComment fields.
    /// Avoids replacing the entire document, so concurrent admin edits on other fields
    /// (e.g. from the edit page) are never overwritten by stale browser data.
    /// </summary>
    public async Task UpdateStatusAsync(
        string id,
        RegistrationStatus status,
        string? statusComment,
        string? withdrawComment,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<Registration>.Update
            .Set(r => r.Status, status)
            .Set(r => r.StatusComment, statusComment)
            .Set(r => r.WithdrawComment, withdrawComment);

        await _collection.UpdateOneAsync(r => r.Id == id, update, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<int> CountByStatusAsync(
        RegistrationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return (int)await _collection.CountDocumentsAsync(
            r => r.Status == status && r.CompetitionYear == competitionYear,
            cancellationToken: cancellationToken);
    }

    public async Task<int> CountByYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return (int)await _collection.CountDocumentsAsync(
            r => r.CompetitionYear == competitionYear,
            cancellationToken: cancellationToken);
    }

    public async Task<int> GetMaxCidSequenceAsync(
        int competitionYear,
        string cidPrefix,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<Registration>.Filter.And(
            Builders<Registration>.Filter.Eq(r => r.CompetitionYear, competitionYear),
            Builders<Registration>.Filter.Regex(r => r.Cid, new BsonRegularExpression($"^{cidPrefix}\\d{{3}}$"))
        );

        var registrations = await _collection.Find(filter).Project(r => r.Cid).ToListAsync(cancellationToken);

        if (!registrations.Any())
            return 0;

        var maxSequence = registrations
            .Where(cid => !string.IsNullOrEmpty(cid))
            .Select(cid =>
            {
                var sequencePart = cid!.Substring(cidPrefix.Length);
                return int.TryParse(sequencePart, out var seq) ? seq : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return maxSequence;
    }

    /// <summary>
    /// Atomically increments and returns the next CID sequence number for a given prefix.
    /// Uses a MongoDB counters collection with FindOneAndUpdate + $inc to guarantee uniqueness
    /// even under concurrent requests. The counter document is keyed by "{year}:{prefix}".
    /// Self-healing: scans existing registrations to find the actual max sequence on first use,
    /// and ensures the counter is always at least as high as the max existing CID sequence.
    /// </summary>
    public async Task<int> GetNextCidSequenceAsync(
        int competitionYear,
        string cidPrefix,
        CancellationToken cancellationToken = default)
    {
        var counterId = $"{competitionYear}:{cidPrefix}";
        var filter = Builders<BsonDocument>.Filter.Eq("_id", counterId);

        // Check if counter exists
        var existing = await _counters.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (existing == null)
        {
            // First time for this prefix — scan existing registrations to find actual max
            var maxSeq = await ScanMaxCidSequenceAsync(competitionYear, cidPrefix, cancellationToken);

            // Seed with SetOnInsert so concurrent calls don't race — only one creates, rest increment
            await _counters.UpdateOneAsync(filter,
                Builders<BsonDocument>.Update.SetOnInsert("seq", maxSeq),
                new UpdateOptions { IsUpsert = true }, cancellationToken);
        }
        else
        {
            // Counter exists — verify it's not behind the actual max (self-healing)
            var currentSeq = existing["seq"].AsInt32;
            var maxSeq = await ScanMaxCidSequenceAsync(competitionYear, cidPrefix, cancellationToken);

            if (maxSeq > currentSeq)
            {
                // Counter is stale — fast-forward it to the actual max.
                // Use $max to avoid racing with concurrent increments.
                await _counters.UpdateOneAsync(filter,
                    Builders<BsonDocument>.Update.Max("seq", maxSeq),
                    cancellationToken: cancellationToken);
            }
        }

        // Now atomically increment and return
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await _counters.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        return result["seq"].AsInt32;
    }

    /// <summary>
    /// Scans all registrations to find the maximum CID sequence number for a given prefix.
    /// CID format is "{prefix}{sequence:D3}", e.g. "M9001", "M91000".
    /// </summary>
    private async Task<int> ScanMaxCidSequenceAsync(
        int competitionYear,
        string cidPrefix,
        CancellationToken cancellationToken)
    {
        var allCids = await _collection
            .Find(r => r.CompetitionYear == competitionYear && r.Cid != null)
            .Project(r => r.Cid!)
            .ToListAsync(cancellationToken);

        var maxSeq = 0;
        foreach (var cid in allCids)
        {
            if (cid.StartsWith(cidPrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(cid[cidPrefix.Length..], out var seq) &&
                seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        return maxSeq;
    }

    public async Task UpdateCidAsync(
        string id,
        string newCid,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<Registration>.Update.Set(r => r.Cid, newCid);
        await _collection.UpdateOneAsync(r => r.Id == id, update, cancellationToken: cancellationToken);
    }
}
