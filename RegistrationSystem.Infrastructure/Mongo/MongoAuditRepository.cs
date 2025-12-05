using MongoDB.Driver;
using RegistrationSystem.Core.Application.Auditing;
using RegistrationSystem.Core.Domain.Auditing;
using RegistrationSystem.Infrastructure.Mongo;

namespace RegistrationSystem.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of IAuditRepository.
/// </summary>
public class MongoAuditRepository : IAuditRepository
{
    private readonly IMongoCollection<AuditEntry> _collection;

    public MongoAuditRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.Database.GetCollection<AuditEntry>("auditEntries");

        // Create indexes for efficient querying
        var indexKeys = Builders<AuditEntry>.IndexKeys;

        // Index for querying by entity
        _collection.Indexes.CreateOne(new CreateIndexModel<AuditEntry>(
            indexKeys.Combine(
                indexKeys.Ascending(x => x.EntityType),
                indexKeys.Ascending(x => x.EntityId),
                indexKeys.Descending(x => x.Timestamp))));

        // Index for querying by date
        _collection.Indexes.CreateOne(new CreateIndexModel<AuditEntry>(
            indexKeys.Descending(x => x.Timestamp)));

        // Index for querying by action type
        _collection.Indexes.CreateOne(new CreateIndexModel<AuditEntry>(
            indexKeys.Combine(
                indexKeys.Ascending(x => x.Action),
                indexKeys.Descending(x => x.Timestamp))));

        // Index for querying by user
        _collection.Indexes.CreateOne(new CreateIndexModel<AuditEntry>(
            indexKeys.Combine(
                indexKeys.Ascending(x => x.UserId),
                indexKeys.Descending(x => x.Timestamp))));

        // Text index for searching summaries and descriptions
        _collection.Indexes.CreateOne(new CreateIndexModel<AuditEntry>(
            indexKeys.Combine(
                indexKeys.Text(x => x.Summary),
                indexKeys.Text(x => x.EntityDescription))));
    }

    public async Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
    }

    public async Task SaveManyAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default)
    {
        var entryList = entries.ToList();
        if (entryList.Count > 0)
        {
            await _collection.InsertManyAsync(entryList, cancellationToken: cancellationToken);
        }
    }

    public async Task<AuditEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetByEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(x => x.EntityType == entityType && x.EntityId == entityId)
            .SortByDescending(x => x.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> SearchAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(criteria);

        return await _collection
            .Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Skip(criteria.Skip)
            .Limit(criteria.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(criteria);
        return (int)await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task<AuditDailyStats> GetDailyStatsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var entries = await _collection
            .Find(x => x.Timestamp >= startUtc && x.Timestamp < endUtc)
            .ToListAsync(cancellationToken);

        return new AuditDailyStats
        {
            Date = date,
            TotalActions = entries.Count,
            ActionCounts = entries
                .GroupBy(x => x.Action)
                .ToDictionary(g => g.Key, g => g.Count()),
            EntityTypeCounts = entries
                .GroupBy(x => x.EntityType)
                .ToDictionary(g => g.Key, g => g.Count()),
            UniqueUsers = entries
                .Where(x => !x.IsSystemAction && !string.IsNullOrEmpty(x.UserId))
                .Select(x => x.UserId)
                .Distinct()
                .Count(),
            SystemActions = entries.Count(x => x.IsSystemAction)
        };
    }

    public async Task<IReadOnlyList<AuditDailyStats>> GetStatsRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var startUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var entries = await _collection
            .Find(x => x.Timestamp >= startUtc && x.Timestamp < endUtc)
            .ToListAsync(cancellationToken);

        // Group by date and calculate stats
        var stats = new List<AuditDailyStats>();
        var current = from;

        while (current <= to)
        {
            var dayStart = current.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd = current.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            var dayEntries = entries
                .Where(x => x.Timestamp >= dayStart && x.Timestamp < dayEnd)
                .ToList();

            stats.Add(new AuditDailyStats
            {
                Date = current,
                TotalActions = dayEntries.Count,
                ActionCounts = dayEntries
                    .GroupBy(x => x.Action)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EntityTypeCounts = dayEntries
                    .GroupBy(x => x.EntityType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                UniqueUsers = dayEntries
                    .Where(x => !x.IsSystemAction && !string.IsNullOrEmpty(x.UserId))
                    .Select(x => x.UserId)
                    .Distinct()
                    .Count(),
                SystemActions = dayEntries.Count(x => x.IsSystemAction)
            });

            current = current.AddDays(1);
        }

        return stats;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private static FilterDefinition<AuditEntry> BuildFilter(AuditSearchCriteria criteria)
    {
        var builder = Builders<AuditEntry>.Filter;
        var filters = new List<FilterDefinition<AuditEntry>>();

        if (!string.IsNullOrEmpty(criteria.EntityType))
        {
            filters.Add(builder.Eq(x => x.EntityType, criteria.EntityType));
        }

        if (!string.IsNullOrEmpty(criteria.EntityId))
        {
            filters.Add(builder.Eq(x => x.EntityId, criteria.EntityId));
        }

        if (criteria.Action.HasValue)
        {
            filters.Add(builder.Eq(x => x.Action, criteria.Action.Value));
        }

        if (criteria.FromDate.HasValue)
        {
            var fromUtc = criteria.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            filters.Add(builder.Gte(x => x.Timestamp, fromUtc));
        }

        if (criteria.ToDate.HasValue)
        {
            var toUtc = criteria.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            filters.Add(builder.Lt(x => x.Timestamp, toUtc));
        }

        if (!string.IsNullOrEmpty(criteria.UserId))
        {
            filters.Add(builder.Eq(x => x.UserId, criteria.UserId));
        }

        if (!string.IsNullOrEmpty(criteria.SearchText))
        {
            // Search in summary and entity description
            var textFilter = builder.Or(
                builder.Regex(x => x.Summary, new MongoDB.Bson.BsonRegularExpression(criteria.SearchText, "i")),
                builder.Regex(x => x.EntityDescription, new MongoDB.Bson.BsonRegularExpression(criteria.SearchText, "i")),
                builder.Regex(x => x.EntityId, new MongoDB.Bson.BsonRegularExpression(criteria.SearchText, "i"))
            );
            filters.Add(textFilter);
        }

        return filters.Count > 0
            ? builder.And(filters)
            : builder.Empty;
    }
}