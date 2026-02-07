using RegistrationSystem.Core.Application.Registrations;
using RegistrationSystem.Core.Domain.CompetitionRounds;
using RegistrationSystem.Core.Domain.Registrations;

namespace RegistrationSystem.Core.Application.CompetitionRounds;

public class CompetitionRoundService
{
    private readonly ICompetitionRoundRepository _roundRepository;
    private readonly IRegistrationRepository _registrationRepository;

    public CompetitionRoundService(
        ICompetitionRoundRepository roundRepository,
        IRegistrationRepository registrationRepository)
    {
        _roundRepository = roundRepository;
        _registrationRepository = registrationRepository;
    }

    // === Video Qualification ===

    public async Task SetVideoQualificationAsync(
        string registrationId,
        VideoQualificationStatus status,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var (registration, round) = await GetOrCreateRoundAsync(registrationId, cancellationToken);
        EnsureRegistrationIsActive(registration);

        round.VideoQualification = status;
        round.VideoQualificationComment = comment;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public Task<IReadOnlyList<CompetitionRound>> GetPendingVideoQualificationsAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByVideoQualificationStatusAsync(
            VideoQualificationStatus.Pending,
            competitionYear,
            cancellationToken);

    // === Screening Round ===

    public async Task AssignScreeningRoundAsync(
        string registrationId,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        var (registration, round) = await GetOrCreateRoundAsync(registrationId, cancellationToken);
        EnsureRegistrationIsActive(registration);

        round.ScreeningRoundDateTime = roundDateTime;
        round.ScreeningRoundAcknowledged = false;
        round.ScreeningRoundAcknowledgedAt = null;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task AcknowledgeScreeningRoundAsync(
        string registrationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (registration.CreatorUserId != userId)
            throw new InvalidOperationException("You do not have permission to acknowledge this round.");

        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("No round assignment found for this registration.");

        if (!round.ScreeningRoundDateTime.HasValue)
            throw new InvalidOperationException("No screening round has been assigned yet.");

        if (round.ScreeningRoundBypass)
            throw new InvalidOperationException("This competitor is exempt from screening.");

        if (round.ScreeningRoundAcknowledged)
            throw new InvalidOperationException("Screening round has already been acknowledged.");

        round.ScreeningRoundAcknowledged = true;
        round.ScreeningRoundAcknowledgedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task SetScreeningRoundResultAsync(
        string registrationId,
        ScreeningRoundStatus status,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("No round assignment found for this registration.");

        if (round.ScreeningRoundBypass)
            throw new InvalidOperationException("This competitor is exempt from screening.");

        round.ScreeningRoundResult = status;
        round.ScreeningRoundComment = comment;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task SetScreeningRoundBypassAsync(
        string registrationId,
        bool bypass,
        CancellationToken cancellationToken = default)
    {
        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("No round found for this registration.");

        round.ScreeningRoundBypass = bypass;

        // If bypassing, clear any existing screening assignment/acknowledgment
        if (bypass)
        {
            round.ScreeningRoundDateTime = null;
            round.ScreeningRoundAcknowledged = false;
            round.ScreeningRoundAcknowledgedAt = null;
            round.ScreeningRoundResult = ScreeningRoundStatus.Pending;
            round.ScreeningRoundComment = null;
        }

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task BulkAssignScreeningRoundAsync(
        IEnumerable<string> registrationIds,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        foreach (var registrationId in registrationIds)
        {
            await AssignScreeningRoundAsync(registrationId, roundDateTime, cancellationToken);
        }
    }

    // === Preliminary Round ===

    public async Task AssignPreliminaryRoundAsync(
        string registrationId,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        var (registration, round) = await GetOrCreateRoundAsync(registrationId, cancellationToken);
        EnsureRegistrationIsActive(registration);

        round.PreliminaryRoundDateTime = roundDateTime;
        round.PreliminaryRoundAcknowledged = false;
        round.PreliminaryRoundAcknowledgedAt = null;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task AcknowledgePreliminaryRoundAsync(
        string registrationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (registration.CreatorUserId != userId)
            throw new InvalidOperationException("You do not have permission to acknowledge this round.");

        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("No round assignment found for this registration.");

        if (!round.HasPreliminaryRound)
            throw new InvalidOperationException("No preliminary round has been assigned yet.");

        if (round.PreliminaryRoundAcknowledged)
            throw new InvalidOperationException("Preliminary round has already been acknowledged.");

        round.PreliminaryRoundAcknowledged = true;
        round.PreliminaryRoundAcknowledgedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task SetPreliminaryRoundResultAsync(
        string registrationId,
        PreliminaryRoundStatus status,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("No round assignment found for this registration.");

        round.PreliminaryRoundResult = status;
        round.PreliminaryRoundComment = comment;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task BulkAssignPreliminaryRoundAsync(
        IEnumerable<string> registrationIds,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        foreach (var registrationId in registrationIds)
        {
            await AssignPreliminaryRoundAsync(registrationId, roundDateTime, cancellationToken);
        }
    }

    // === Final Round ===

    public async Task AssignFinalRoundAsync(
        string registrationId,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        var (registration, round) = await GetOrCreateRoundAsync(registrationId, cancellationToken);
        EnsureRegistrationIsActive(registration);

        if (round.VideoQualification == VideoQualificationStatus.Fail)
            throw new InvalidOperationException("Registrations with failed video qualification cannot be assigned to final rounds.");

        round.FinalRoundDateTime = roundDateTime;
        round.FinalRoundAcknowledged = false;
        round.FinalRoundAcknowledgedAt = null;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task AcknowledgeFinalRoundAsync(
        string registrationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (registration.CreatorUserId != userId)
            throw new InvalidOperationException("You do not have permission to acknowledge this round.");

        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("No round assignment found for this registration.");

        if (!round.HasFinalRound)
            throw new InvalidOperationException("No final round has been assigned yet.");

        if (round.FinalRoundAcknowledged)
            throw new InvalidOperationException("Final round has already been acknowledged.");

        round.FinalRoundAcknowledged = true;
        round.FinalRoundAcknowledgedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task SetFinalRoundResultAsync(
        string registrationId,
        FinalRoundStatus status,
        int? placement = null,
        CancellationToken cancellationToken = default)
    {
        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("No round assignment found for this registration.");

        round.FinalRoundResult = status;
        round.FinalRoundPlacement = placement;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    public async Task BulkAssignFinalRoundAsync(
        IEnumerable<string> registrationIds,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        foreach (var registrationId in registrationIds)
        {
            await AssignFinalRoundAsync(registrationId, roundDateTime, cancellationToken);
        }
    }

    // === Queries ===

    public Task<IReadOnlyList<CompetitionRound>> GetPendingAcknowledgmentsAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetWithPendingAcknowledgmentsAsync(competitionYear, cancellationToken);

    public Task<CompetitionRound?> GetByRegistrationIdAsync(
        string registrationId,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken);

    public async Task<IReadOnlyDictionary<string, CompetitionRound>> GetByRegistrationIdsAsync(
        IEnumerable<string> registrationIds,
        CancellationToken cancellationToken = default)
    {
        var rounds = await _roundRepository.GetByRegistrationIdsAsync(registrationIds, cancellationToken);
        return rounds.ToDictionary(r => r.RegistrationId, r => r);
    }

    public Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByPreliminaryRoundDateAsync(roundDate, competitionYear, cancellationToken);

    public Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByFinalRoundDateAsync(roundDate, competitionYear, cancellationToken);

    public Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByPreliminaryRoundDateTimeAsync(roundDateTime, competitionYear, cancellationToken);

    public Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByFinalRoundDateTimeAsync(roundDateTime, competitionYear, cancellationToken);

    public Task<IReadOnlyList<CompetitionRound>> GetAllByYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByCompetitionYearAsync(competitionYear, cancellationToken);

    // === Private Helpers ===

    private async Task<(Registration registration, CompetitionRound round)> GetOrCreateRoundAsync(
        string registrationId,
        CancellationToken cancellationToken)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? new CompetitionRound
            {
                RegistrationId = registrationId,
                CompetitionYear = registration.CompetitionYear,
                DivisionId = registration.CompetitionSelection.DivisionId,
                CategoryId = registration.CompetitionSelection.CategoryId,
                Cid = registration.Cid,
                CompetitorName = registration.PersonalInfo.FullName
            };

        return (registration, round);
    }

    private static void EnsureRegistrationIsActive(Registration registration)
    {
        if (registration.Status != RegistrationStatus.Reviewed &&
            registration.Status != RegistrationStatus.Verified)
            throw new InvalidOperationException("Only reviewed or verified registrations can be assigned to rounds.");
    }

    public Task<IReadOnlyList<CompetitionRound>> GetByCategoryAsync(
    string categoryId,
    int competitionYear,
    CancellationToken cancellationToken = default)
    => _roundRepository.GetByCategoryAsync(categoryId, competitionYear, cancellationToken);

    // === Updates === (add as new section)

    /// <summary>
    /// Updates an existing competition round. Used for bulk updates like updating messages.
    /// </summary>
    public Task UpdateAsync(
        CompetitionRound round,
        CancellationToken cancellationToken = default)
        => _roundRepository.SaveAsync(round, cancellationToken);


}