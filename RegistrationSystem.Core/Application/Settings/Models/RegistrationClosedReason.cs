namespace RegistrationSystem.Core.Application.Settings;

/// <summary>
/// Reason why registration is closed for a category.
/// Ordered by hierarchy priority (global → division → category → dates).
/// </summary>
public enum RegistrationClosedReason
{
    Open = 0,
    GloballyDisabled = 1,
    DivisionDisabled = 2,
    CategoryDisabled = 3,
    NotStarted = 4,
    Ended = 5
}
