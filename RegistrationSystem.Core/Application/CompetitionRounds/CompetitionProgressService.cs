using RegistrationSystem.Core.Application.Registrations;
using RegistrationSystem.Core.Domain.CompetitionRounds;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.CompetitionRounds;

public class CompetitionProgressService
{
    private readonly ICompetitionProgressRepository _repository;
    private readonly IRegistrationRepository _registrationRepository;

    public CompetitionProgressService(
        ICompetitionProgressRepository repository,
        IRegistrationRepository registrationRepository)
    {
        _repository = repository;
        _registrationRepository = registrationRepository;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a CompetitionProgress document for a registration based on the
    /// category's round definitions. First round starts as Active, rest are Pending.
    /// </summary>
    public async Task<CompetitionProgress> InitializeAsync(
        Registration registration,
        List<RoundDefinition> roundDefinitions,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByRegistrationIdAsync(registration.Id, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("Competition progress already exists for this registration.");

        var exempt = registration.ScreeningExempt == true;
        var orderedRounds = roundDefinitions.OrderBy(rd => rd.Order).ToList();

        var progress = new CompetitionProgress
        {
            RegistrationId = registration.Id,
            CompetitionYear = registration.CompetitionYear,
            DivisionId = registration.CompetitionSelection.DivisionId,
            CategoryId = registration.CompetitionSelection.CategoryId,
            Cid = registration.Cid,
            CompetitorName = registration.PersonalInfo.FullName,
            Rounds = orderedRounds
                .Select((rd, index) => new RoundEntry
                {
                    RoundDefinitionId = rd.Id,
                    Order = rd.Order,
                    Name = rd.Name,
                    // Exempt competitors skip the first round (screening); second round starts Active.
                    Status = exempt
                        ? (index == 0 ? RoundEntryStatus.Bypassed
                            : index == 1 ? RoundEntryStatus.Active
                            : RoundEntryStatus.Pending)
                        : (index == 0 ? RoundEntryStatus.Active : RoundEntryStatus.Pending),
                    // Exempt: bypass = red dot (they should see "you're exempt"); auto-activated next round = no red dot yet.
                    // Non-exempt: first round Active = red dot ("go check your progress").
                    Acknowledged = exempt && index == 1,
                    Bypassed = exempt && index == 0,
                    BypassReason = exempt && index == 0 ? "Returning finalist" : null
                })
                .ToList()
        };

        await _repository.SaveAsync(progress, cancellationToken);
        return progress;
    }

    /// <summary>
    /// Syncs existing CompetitionProgress records for a category against the current round
    /// definitions from settings. Safe to run at any time — does not lose results or status.
    ///
    /// For each existing progress record:
    ///   - Updates Name and Order on rounds that still exist in settings.
    ///   - Adds any new rounds from settings that are missing, inserted as Pending.
    ///   - Removes orphaned rounds (deleted from settings) only when Status == Pending and no result.
    ///   - If the competitor has no Active round after sync (e.g., previously bypassed the only
    ///     round and now new rounds exist), activates the next Pending round.
    ///
    /// Returns the number of progress records updated.
    /// </summary>
    public async Task<int> SyncCategoryRoundsAsync(
        string categoryId,
        int competitionYear,
        List<RoundDefinition> roundDefinitions,
        CancellationToken cancellationToken = default)
    {
        var allProgress = await _repository.GetByCategoryAsync(categoryId, competitionYear, cancellationToken);
        if (allProgress.Count == 0) return 0;

        var settingsRounds = roundDefinitions.OrderBy(rd => rd.Order).ToList();
        var settingsIds = settingsRounds.Select(rd => rd.Id).ToHashSet();

        var count = 0;
        foreach (var progress in allProgress)
        {
            var changed = false;

            // 1. Update name/order on rounds that still exist in settings
            foreach (var entry in progress.Rounds)
            {
                var def = settingsRounds.FirstOrDefault(rd => rd.Id == entry.RoundDefinitionId);
                if (def is null) continue;
                if (entry.Name != def.Name || entry.Order != def.Order)
                {
                    entry.Name = def.Name;
                    entry.Order = def.Order;
                    changed = true;
                }
            }

            // 2. Remove orphaned rounds (only safe to remove if Pending and no result)
            var orphans = progress.Rounds
                .Where(e => !settingsIds.Contains(e.RoundDefinitionId)
                            && e.Status == RoundEntryStatus.Pending
                            && e.Result is null)
                .ToList();
            foreach (var orphan in orphans)
            {
                progress.Rounds.Remove(orphan);
                changed = true;
            }

            // 3. Add missing rounds from settings
            var existingIds = progress.Rounds.Select(e => e.RoundDefinitionId).ToHashSet();
            foreach (var def in settingsRounds.Where(rd => !existingIds.Contains(rd.Id)))
            {
                progress.Rounds.Add(new RoundEntry
                {
                    RoundDefinitionId = def.Id,
                    Order = def.Order,
                    Name = def.Name,
                    Status = RoundEntryStatus.Pending
                });
                changed = true;
            }

            if (!changed) continue;

            // 4. Re-sort rounds by order
            progress.Rounds = progress.Rounds.OrderBy(e => e.Order).ToList();

            // 5. If no Active round exists, activate the next Pending round after the last
            //    completed/bypassed/eliminated entry (so returning finalists advance correctly)
            var hasActive = progress.Rounds.Any(e => e.Status == RoundEntryStatus.Active);
            if (!hasActive)
            {
                var lastDoneOrder = progress.Rounds
                    .Where(e => e.Status is RoundEntryStatus.Completed
                                           or RoundEntryStatus.Bypassed
                                           or RoundEntryStatus.Eliminated)
                    .Select(e => (int?)e.Order)
                    .Max();

                var nextPending = progress.Rounds
                    .Where(e => e.Status == RoundEntryStatus.Pending
                                && (lastDoneOrder is null || e.Order > lastDoneOrder))
                    .OrderBy(e => e.Order)
                    .FirstOrDefault();

                if (nextPending is not null)
                {
                    nextPending.Status = RoundEntryStatus.Active;
                }
            }

            await _repository.SaveAsync(progress, cancellationToken);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Bulk-initializes competition progress for all verified registrations in a category
    /// that don't already have progress. Returns the count of newly initialized competitors.
    /// </summary>
    public async Task<int> InitializeCategoryAsync(
        string categoryId,
        int competitionYear,
        List<RoundDefinition> roundDefinitions,
        CancellationToken cancellationToken = default)
    {
        var registrations = await _registrationRepository.GetByCompetitionYearAsync(competitionYear, cancellationToken);
        var verified = registrations
            .Where(r => r.CompetitionSelection.CategoryId == categoryId && r.Status == RegistrationStatus.Verified)
            .ToList();

        var existing = await _repository.GetByCategoryAsync(categoryId, competitionYear, cancellationToken);
        var existingRegIds = existing.Select(p => p.RegistrationId).ToHashSet();

        var count = 0;
        foreach (var reg in verified.Where(r => !existingRegIds.Contains(r.Id)))
        {
            await InitializeAsync(reg, roundDefinitions, cancellationToken);
            count++;
        }
        return count;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ROUND RESULTS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the result for a specific round. Advancing results (Pass, Qualified, Placed, Participated)
    /// move the next round to Active. Eliminating results (Fail, NotQualified, NoShow) stop progression.
    /// </summary>
    public async Task SetRoundResultAsync(
        string registrationId,
        string roundDefinitionId,
        RoundResult result,
        string? comment = null,
        int? placement = null,
        CancellationToken cancellationToken = default)
    {
        var progress = await _repository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Competition progress not found.");

        var round = progress.GetRound(roundDefinitionId)
            ?? throw new InvalidOperationException($"Round '{roundDefinitionId}' not found.");

        if (round.Status != RoundEntryStatus.Active)
            throw new InvalidOperationException($"Round '{round.Name}' is not active (status: {round.Status}).");

        round.Result = result;
        round.Comment = comment;
        round.Placement = placement;
        round.Acknowledged = false; // Result is news — triggers red dot notification

        if (IsAdvancingResult(result))
        {
            round.Status = RoundEntryStatus.Completed;
            AdvanceToNextRound(progress, round.Order);
        }
        else
        {
            round.Status = RoundEntryStatus.Eliminated;
        }

        await _repository.SaveAsync(progress, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SCHEDULING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Assigns a scheduled date/time to a round for a single competitor.
    /// Only valid for rounds where RoundDefinition.HasSchedule = true.
    /// </summary>
    public async Task AssignScheduleAsync(
        string registrationId,
        string roundDefinitionId,
        DateTimeOffset dateTime,
        CancellationToken cancellationToken = default)
    {
        var progress = await _repository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Competition progress not found.");

        var round = progress.GetRound(roundDefinitionId)
            ?? throw new InvalidOperationException($"Round '{roundDefinitionId}' not found.");

        round.ScheduledDateTime = dateTime;
        round.Acknowledged = false;
        round.AcknowledgedAt = null;

        await _repository.SaveAsync(progress, cancellationToken);
    }

    /// <summary>
    /// Bulk-assigns a scheduled date/time to a round for multiple competitors.
    /// </summary>
    public async Task BulkAssignScheduleAsync(
        IEnumerable<string> registrationIds,
        string roundDefinitionId,
        DateTimeOffset dateTime,
        CancellationToken cancellationToken = default)
    {
        foreach (var registrationId in registrationIds)
        {
            await AssignScheduleAsync(registrationId, roundDefinitionId, dateTime, cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BYPASS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bypasses (or un-bypasses) a round for a competitor. When bypassed,
    /// the round is marked as Bypassed and the next round becomes Active.
    /// </summary>
    public async Task SetBypassAsync(
        string registrationId,
        string roundDefinitionId,
        bool bypass,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var progress = await _repository.GetByRegistrationIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Competition progress not found.");

        var round = progress.GetRound(roundDefinitionId)
            ?? throw new InvalidOperationException($"Round '{roundDefinitionId}' not found.");

        if (bypass)
        {
            round.Bypassed = true;
            round.BypassReason = reason;
            round.Status = RoundEntryStatus.Bypassed;
            round.ScheduledDateTime = null;
            round.Acknowledged = false;
            round.AcknowledgedAt = null;

            AdvanceToNextRound(progress, round.Order);
        }
        else
        {
            // Un-bypass: revert to Active if it was Bypassed
            if (round.Status == RoundEntryStatus.Bypassed)
            {
                round.Bypassed = false;
                round.BypassReason = null;
                round.Status = RoundEntryStatus.Active;

                // Revert any rounds after this one back to Pending
                foreach (var laterRound in progress.Rounds.Where(r => r.Order > round.Order))
                {
                    if (laterRound.Status == RoundEntryStatus.Active)
                        laterRound.Status = RoundEntryStatus.Pending;
                }
            }
        }

        await _repository.SaveAsync(progress, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ACKNOWLEDGMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Silently acknowledges any unacknowledged bypassed rounds.
    /// Called automatically when the user visits their messages page — no button required.
    /// Bypassed = "you're exempt" which is self-explanatory; viewing the page is enough.
    /// </summary>
    public async Task AcknowledgeBypassedRoundsAsync(
        string registrationId,
        CancellationToken cancellationToken = default)
    {
        var progress = await _repository.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (progress == null) return;

        var bypassed = progress.Rounds
            .Where(r => r.Status == RoundEntryStatus.Bypassed && !r.Acknowledged)
            .ToList();

        if (bypassed.Count == 0) return;

        foreach (var round in bypassed)
        {
            round.Acknowledged = true;
            round.AcknowledgedAt = DateTimeOffset.UtcNow;
        }

        await _repository.SaveAsync(progress, cancellationToken);
    }

    /// <summary>
    /// Auto-acknowledges the first unacknowledged round for a registration.
    /// Called when the user (not admin) visits their tracking page.
    /// </summary>
    public async Task AcknowledgeAsync(
        string registrationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var progress = await _repository.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (progress == null) return;

        // Verify the user owns this registration
        var registration = await _registrationRepository.GetByIdAsync(registrationId, cancellationToken);
        if (registration == null || registration.CreatorUserId != userId) return;

        var changed = false;
        foreach (var round in progress.Rounds.Where(r =>
            !r.Acknowledged && r.Status != RoundEntryStatus.Pending))
        {
            round.Acknowledged = true;
            round.AcknowledgedAt = DateTimeOffset.UtcNow;
            changed = true;
        }

        if (changed)
            await _repository.SaveAsync(progress, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // QUERIES
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<CompetitionProgress?> GetByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default) =>
        await _repository.GetByRegistrationIdAsync(registrationId, cancellationToken);

    public async Task<Dictionary<string, CompetitionProgress>> GetByRegistrationIdsAsync(
        IEnumerable<string> registrationIds, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByRegistrationIdsAsync(registrationIds, cancellationToken);
        return list.ToDictionary(p => p.RegistrationId);
    }

    public async Task<IReadOnlyList<CompetitionProgress>> GetByCategoryAsync(
        string categoryId, int year, CancellationToken cancellationToken = default) =>
        await _repository.GetByCategoryAsync(categoryId, year, cancellationToken);

    public async Task<IReadOnlyList<CompetitionProgress>> GetAllByYearAsync(
        int year, CancellationToken cancellationToken = default) =>
        await _repository.GetByCompetitionYearAsync(year, cancellationToken);

    /// <summary>
    /// Deletes the CompetitionProgress record for a specific registration.
    /// Called when a registration is deleted to keep data consistent.
    /// </summary>
    public async Task DeleteByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default) =>
        await _repository.DeleteByRegistrationIdAsync(registrationId, cancellationToken);

    /// <summary>
    /// Deletes all CompetitionProgress records for a category and year.
    /// Use this to reset a category before re-initializing with updated round definitions.
    /// Returns the number of deleted records.
    /// </summary>
    public async Task<int> ResetCategoryAsync(
        string categoryId,
        int competitionYear,
        CancellationToken cancellationToken = default) =>
        await _repository.DeleteByCategoryAsync(categoryId, competitionYear, cancellationToken);

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private static bool IsAdvancingResult(RoundResult result) =>
        result is RoundResult.Pass or RoundResult.Qualified or RoundResult.Placed or RoundResult.Participated;

    private static void AdvanceToNextRound(CompetitionProgress progress, int currentOrder)
    {
        var nextRound = progress.Rounds
            .Where(r => r.Order > currentOrder)
            .OrderBy(r => r.Order)
            .FirstOrDefault();

        if (nextRound != null && nextRound.Status == RoundEntryStatus.Pending)
        {
            nextRound.Status = RoundEntryStatus.Active;
            nextRound.Acknowledged = true; // Activating next round is not news — red dot only fires when there's a result or scheduling event
        }
    }
}
