using MongoDB.Driver;
using RegistrationSystem.Core.Application.Scheduling;
using RegistrationSystem.Core.Domain.Scheduling;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoSchedulingBookingRepository : ISchedulingBookingRepository
{
    private readonly IMongoCollection<SchedulingBooking> _collection;

    public MongoSchedulingBookingRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.SchedulingBookings;
    }

    public async Task<SchedulingBooking?> GetByIdAsync(
        string id, CancellationToken cancellationToken = default) =>
        await _collection.Find(b => b.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<SchedulingBooking?> GetActiveByRegistrationAndSessionAsync(
        string registrationId, string sessionId, CancellationToken cancellationToken = default) =>
        await _collection
            .Find(b => b.RegistrationId == registrationId &&
                       b.SessionId == sessionId &&
                       b.Status == BookingStatus.Active)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SchedulingBooking>> GetBySessionAsync(
        string sessionId, CancellationToken cancellationToken = default) =>
        await _collection.Find(b => b.SessionId == sessionId)
            .SortBy(b => b.Date).ThenBy(b => b.TimeUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SchedulingBooking>> GetByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default) =>
        await _collection.Find(b => b.RegistrationId == registrationId)
            .SortByDescending(b => b.BookedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> CountActiveBySlotAsync(
        string sessionId, string slotId, CancellationToken cancellationToken = default)
    {
        var count = await _collection.CountDocumentsAsync(
            b => b.SessionId == sessionId &&
                 b.SlotId == slotId &&
                 b.Status == BookingStatus.Active,
            cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task<Dictionary<string, int>> GetSlotBookingCountsAsync(
        string sessionId, CancellationToken cancellationToken = default)
    {
        var activeBookings = await _collection
            .Find(b => b.SessionId == sessionId && b.Status == BookingStatus.Active)
            .ToListAsync(cancellationToken);

        return activeBookings
            .GroupBy(b => b.SlotId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task SaveAsync(
        SchedulingBooking booking, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(booking.Id))
        {
            booking.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            await _collection.InsertOneAsync(booking, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(
                b => b.Id == booking.Id,
                booking,
                new ReplaceOptions { IsUpsert = false },
                cancellationToken);
        }
    }

    public async Task CancelAsync(
        string id, string? reason, CancellationToken cancellationToken = default)
    {
        var update = Builders<SchedulingBooking>.Update
            .Set(b => b.Status, BookingStatus.Cancelled)
            .Set(b => b.CancelledAt, DateTimeOffset.UtcNow)
            .Set(b => b.CancellationReason, reason);
        await _collection.UpdateOneAsync(b => b.Id == id, update, cancellationToken: cancellationToken);
    }

    public async Task CancelAllByRegistrationIdAsync(
        string registrationId, string? reason, CancellationToken cancellationToken = default)
    {
        var update = Builders<SchedulingBooking>.Update
            .Set(b => b.Status, BookingStatus.Cancelled)
            .Set(b => b.CancelledAt, DateTimeOffset.UtcNow)
            .Set(b => b.CancellationReason, reason);
        await _collection.UpdateManyAsync(
            b => b.RegistrationId == registrationId && b.Status == BookingStatus.Active,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SchedulingBooking>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true)
            .SortByDescending(b => b.BookedAt)
            .ToListAsync(cancellationToken);

    public async Task DeleteAllByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default) =>
        await _collection.DeleteManyAsync(
            b => b.RegistrationId == registrationId,
            cancellationToken);
}
