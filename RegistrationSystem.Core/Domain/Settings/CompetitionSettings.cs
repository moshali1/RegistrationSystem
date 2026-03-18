using RegistrationSystem.Core.Domain.Scheduling;

namespace RegistrationSystem.Core.Domain.Settings;

public class CompetitionSettings
{
    public const string SingletonId = "default-competition-settings";

    public string Id { get; set; } = SingletonId;

    public bool RegistrationEnabled { get; set; }
    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }
    public DateTimeOffset? PendingDeadline { get; set; }
    public DateOnly AgeCutoffDate { get; set; } = new DateOnly(DateTime.UtcNow.Year, 1, 1);

    public List<Division> Divisions { get; set; } = new();
    public CompetitionInfo CompetitionInfo { get; set; } = new();
    public CidConfiguration CidConfiguration { get; set; } = new();
    public EmailDefaults EmailDefaults { get; set; } = new();

    /// <summary>
    /// Scheduling sessions configured for competition rounds.
    /// Sessions may be grouped with a GroupId to represent parallel sections (A, B, C...)
    /// for the same round. Participants book into a specific session and slot via the
    /// scheduling page; bookings are stored in the SchedulingBookings collection.
    /// </summary>
    public List<SchedulingSession> SchedulingSessions { get; set; } = new();

    public Division? FindDivision(string divisionId) =>
        Divisions.FirstOrDefault(d => d.Id == divisionId);
}
