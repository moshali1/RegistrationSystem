using MongoDB.Driver;
using RegistrationSystem.Core.Domain.Users;

namespace RegistrationSystem.Infrastructure.Mongo;

public class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _collection;

    public MongoUserRepository(MongoRegistrationSystemContext context)
    {
        _collection = context.Users;
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetByObjectIdentifierAsync(string objectIdentifier, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(u => u.ObjectIdentifier == objectIdentifier)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(u => u.Email.ToLower() == email.ToLower())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(User user, CancellationToken cancellationToken = default)
    {
        var existingUser = await GetByIdAsync(user.Id, cancellationToken);

        if (existingUser is null)
        {
            await _collection.InsertOneAsync(user, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(
                u => u.Id == user.Id,
                user,
                cancellationToken: cancellationToken);
        }
    }
}