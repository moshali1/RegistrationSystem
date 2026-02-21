using RegistrationSystem.Core.Domain.NiqabBypasses;

namespace RegistrationSystem.Core.Application.NiqabBypasses;

/// <summary>
/// Repository interface for NiqabBypass entities.
/// </summary>
public interface INiqabBypassRepository
{
    /// <summary>
    /// Gets a bypass by its code.
    /// </summary>
    Task<NiqabBypass?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all bypasses for a competition year.
    /// </summary>
    Task<IReadOnlyList<NiqabBypass>> GetByYearAsync(int competitionYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an unused bypass exists for the given competitor.
    /// </summary>
    Task<NiqabBypass?> FindUnusedBypassAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a claimed (used) bypass for the given competitor.
    /// Used during registration to auto-apply bypass.
    /// </summary>
    Task<NiqabBypass?> FindClaimedBypassAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a bypass (insert or update).
    /// </summary>
    Task SaveAsync(NiqabBypass bypass, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bypass by ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a bypass linked to a specific registration.
    /// </summary>
    Task<NiqabBypass?> FindByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing niqab bypasses.
/// </summary>
public class NiqabBypassService
{
    private readonly INiqabBypassRepository _repository;

    public NiqabBypassService(INiqabBypassRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Creates a new niqab bypass for a competitor.
    /// </summary>
    public async Task<NiqabBypass> CreateBypassAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        string? createdBy = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        // Check if an unused bypass already exists
        var existing = await _repository.FindUnusedBypassAsync(
            firstName, lastName, dateOfBirth, competitionYear, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException(
                $"An unused bypass already exists for this competitor (code: {existing.Code}).");
        }

        var bypass = new NiqabBypass
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            DateOfBirth = dateOfBirth,
            Code = NiqabBypass.GenerateCode(),
            CompetitionYear = competitionYear,
            CreatedBy = createdBy,
            Notes = notes,
            IsUsed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.SaveAsync(bypass, cancellationToken);
        return bypass;
    }

    /// <summary>
    /// Validates and claims a bypass code.
    /// Returns the bypass if valid, or null with an error message.
    /// </summary>
    public async Task<(NiqabBypass? Bypass, string? Error)> ValidateAndClaimAsync(
        string code,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        var bypass = await _repository.GetByCodeAsync(code, cancellationToken);

        if (bypass == null)
        {
            return (null, "Invalid bypass code. Please check the code and try again.");
        }

        if (bypass.IsUsed)
        {
            return (null, "This bypass code has already been used.");
        }

        if (!bypass.MatchesCompetitor(firstName, lastName, dateOfBirth))
        {
            return (null, "This bypass code is not valid for the provided name and date of birth.");
        }

        // Mark as used
        bypass.MarkAsUsed();
        await _repository.SaveAsync(bypass, cancellationToken);

        return (bypass, null);
    }

    /// <summary>
    /// Checks if a claimed bypass exists for a competitor.
    /// Used during registration to auto-apply bypass.
    /// </summary>
    public async Task<NiqabBypass?> FindValidBypassAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        // Look for a bypass that has been claimed (IsUsed = true)
        return await _repository.FindClaimedBypassAsync(
            firstName, lastName, dateOfBirth, competitionYear, cancellationToken);
    }

    /// <summary>
    /// Gets all bypasses for a competition year (for admin view).
    /// </summary>
    public async Task<IReadOnlyList<NiqabBypass>> GetBypassesByYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByYearAsync(competitionYear, cancellationToken);
    }

    /// <summary>
    /// Saves an updated bypass (admin only).
    /// </summary>
    public async Task SaveBypassAsync(NiqabBypass bypass, CancellationToken cancellationToken = default)
    {
        await _repository.SaveAsync(bypass, cancellationToken);
    }

    /// <summary>
    /// Deletes a bypass (admin only).
    /// </summary>
    public async Task DeleteBypassAsync(string id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Finds a bypass linked to a specific registration ID.
    /// </summary>
    public async Task<NiqabBypass?> FindByRegistrationIdAsync(
        string registrationId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.FindByRegistrationIdAsync(registrationId, cancellationToken);
    }

    /// <summary>
    /// Creates a reverse bypass — documenting that a competitor's face was detected
    /// through niqab by AI and needs in-person identity verification.
    /// The bypass is created as already used since it's retroactive documentation.
    /// </summary>
    public async Task<NiqabBypass> CreateReverseBypassAsync(
        string registrationId,
        string registrationCid,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        string? createdBy = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        // Check if a reverse bypass already exists for this registration
        var existing = await _repository.FindByRegistrationIdAsync(registrationId, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException(
                $"A bypass record already exists for this registration (code: {existing.Code}).");
        }

        var bypass = new NiqabBypass
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            DateOfBirth = dateOfBirth,
            Code = NiqabBypass.GenerateCode(),
            CompetitionYear = competitionYear,
            CreatedBy = createdBy,
            Notes = notes ?? "Reverse bypass — face detected through niqab, flagged for in-person verification.",
            IsUsed = true,
            IsReverse = true,
            RegistrationId = registrationId,
            RegistrationCid = registrationCid,
            UsedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.SaveAsync(bypass, cancellationToken);
        return bypass;
    }
}
