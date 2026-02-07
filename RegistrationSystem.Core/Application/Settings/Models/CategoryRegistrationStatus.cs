namespace RegistrationSystem.Core.Application.Settings;

public class CategoryRegistrationStatus
{
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string DivisionId { get; init; } = string.Empty;
    public string DivisionName { get; init; } = string.Empty;

    public bool IsOpen { get; init; }
    public RegistrationClosedReason Reason { get; init; }
    public DateTimeOffset? EffectiveStart { get; init; }
    public DateTimeOffset? EffectiveEnd { get; init; }
    public bool HasOverride { get; init; }
    public bool IsManuallyEnabled { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
}