using RegistrationSystem.Core.Domain.CompetitionRounds;
using RegistrationSystem.Core.Domain.Registrations;

namespace RegistrationSystem.Core.Application.CompetitionRounds;

/// <summary>
/// Service for managing competition rounds and video qualifications.
/// </summary>
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

    #region Video Qualification

    /// <summary>
    /// Sets video qualification status for a registration (admin only).
    /// Creates CompetitionRound record if it doesn't exist.
    /// </summary>
    public async Task SetVideoQualificationAsync(
        string registrationId,
        VideoQualificationStatus status,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (registration.Status != RegistrationStatus.Reviewed &&
            registration.Status != RegistrationStatus.Verified)
            throw new InvalidOperationException("Only reviewed or verified registrations can have video qualification assessed.");

        // Get or create competition round
        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken);

        if (round == null)
        {
            round = new CompetitionRound
            {
                RegistrationId = registrationId,
                CompetitionYear = registration.CompetitionYear,
                DivisionId = registration.CompetitionSelection.DivisionId,
                CategoryId = registration.CompetitionSelection.CategoryId,
                Cid = registration.Cid,
                CompetitorName = registration.PersonalInfo.FullName,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        round.VideoQualification = status;
        round.VideoQualificationAssessedAt = DateTimeOffset.UtcNow;
        round.VideoQualificationComment = comment;
        round.UpdatedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    /// <summary>
    /// Gets all registrations with pending video qualification.
    /// </summary>
    public Task<IReadOnlyList<CompetitionRound>> GetPendingVideoQualificationsAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByVideoQualificationStatusAsync(
            VideoQualificationStatus.Pending,
            competitionYear,
            cancellationToken);

    #endregion

    #region Round Assignment

    /// <summary>
    /// Assigns preliminary round date/time to a registration (admin only).
    /// Creates CompetitionRound record if it doesn't exist.
    /// </summary>
    public async Task AssignPreliminaryRoundAsync(
        string registrationId,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (registration.Status != RegistrationStatus.Reviewed &&
            registration.Status != RegistrationStatus.Verified)
            throw new InvalidOperationException("Only reviewed or verified registrations can be assigned to rounds.");

        // Get or create competition round
        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken);

        if (round == null)
        {
            round = new CompetitionRound
            {
                RegistrationId = registrationId,
                CompetitionYear = registration.CompetitionYear,
                DivisionId = registration.CompetitionSelection.DivisionId,
                CategoryId = registration.CompetitionSelection.CategoryId,
                Cid = registration.Cid,
                CompetitorName = registration.PersonalInfo.FullName,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        round.PreliminaryRoundDateTime = roundDateTime;
        round.PreliminaryRoundAcknowledged = false;
        round.PreliminaryRoundAcknowledgedAt = null;
        round.UpdatedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    /// <summary>
    /// Assigns final round date/time to a registration (admin only).
    /// Creates CompetitionRound record if it doesn't exist.
    /// </summary>
    public async Task AssignFinalRoundAsync(
        string registrationId,
        DateTimeOffset roundDateTime,
        CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        // Get or create competition round
        var round = await _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken);

        if (round == null)
        {
            round = new CompetitionRound
            {
                RegistrationId = registrationId,
                CompetitionYear = registration.CompetitionYear,
                DivisionId = registration.CompetitionSelection.DivisionId,
                CategoryId = registration.CompetitionSelection.CategoryId,
                Cid = registration.Cid,
                CompetitorName = registration.PersonalInfo.FullName,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        // Validate video qualification if exists
        if (round.VideoQualification == VideoQualificationStatus.Fail)
            throw new InvalidOperationException("Registrations with failed video qualification cannot be assigned to final rounds.");

        round.FinalRoundDateTime = roundDateTime;
        round.FinalRoundAcknowledged = false;
        round.FinalRoundAcknowledgedAt = null;
        round.UpdatedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    /// <summary>
    /// Bulk assigns preliminary round to multiple registrations.
    /// </summary>
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

    /// <summary>
    /// Bulk assigns final round to multiple registrations.
    /// </summary>
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

    #endregion

    #region Acknowledgments

    /// <summary>
    /// Acknowledges preliminary round assignment (user action).
    /// </summary>
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
        round.UpdatedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    /// <summary>
    /// Acknowledges final round assignment (user action).
    /// </summary>
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
        round.UpdatedAt = DateTimeOffset.UtcNow;

        await _roundRepository.SaveAsync(round, cancellationToken);
    }

    /// <summary>
    /// Gets all rounds with unacknowledged assignments.
    /// </summary>
    public Task<IReadOnlyList<CompetitionRound>> GetPendingAcknowledgmentsAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetWithPendingAcknowledgmentsAsync(competitionYear, cancellationToken);

    #endregion

    #region Queries

    /// <summary>
    /// Gets competition round for a registration.
    /// </summary>
    public Task<CompetitionRound?> GetByRegistrationIdAsync(
        string registrationId,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByRegistrationIdAsync(registrationId, cancellationToken);

    /// <summary>
    /// Gets all rounds for a specific preliminary round date.
    /// </summary>
    public Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByPreliminaryRoundDateAsync(roundDate, competitionYear, cancellationToken);

    /// <summary>
    /// Gets all rounds for a specific final round date.
    /// </summary>
    public Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByFinalRoundDateAsync(roundDate, competitionYear, cancellationToken);

    /// <summary>
    /// Gets all rounds for a specific preliminary round date/time slot.
    /// </summary>
    public Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByPreliminaryRoundDateTimeAsync(roundDateTime, competitionYear, cancellationToken);

    /// <summary>
    /// Gets all rounds for a specific final round date/time slot.
    /// </summary>
    public Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByFinalRoundDateTimeAsync(roundDateTime, competitionYear, cancellationToken);

    /// <summary>
    /// Gets all rounds for a competition year.
    /// </summary>
    public Task<IReadOnlyList<CompetitionRound>> GetAllByYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _roundRepository.GetByCompetitionYearAsync(competitionYear, cancellationToken);

    #endregion
}