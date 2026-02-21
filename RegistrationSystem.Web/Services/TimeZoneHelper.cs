namespace RegistrationSystem.Web.Services;

/// <summary>
/// Centralized timezone helper for consistent Central Time display across the app.
/// All DateTimeOffset values stored in UTC are converted to Central Time for display.
/// </summary>
public static class TimeZoneHelper
{
    private static readonly TimeZoneInfo CentralTz =
        TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    /// <summary>
    /// Converts a DateTimeOffset to Central Time DateTime.
    /// </summary>
    public static DateTime ToCentralTime(this DateTimeOffset dto)
        => TimeZoneInfo.ConvertTimeFromUtc(dto.UtcDateTime, CentralTz);

    /// <summary>
    /// Formats a DateTimeOffset as Central Time with the given format string.
    /// </summary>
    public static string FormatCentral(this DateTimeOffset dto, string format)
        => dto.ToCentralTime().ToString(format);

    /// <summary>
    /// Formats a nullable DateTimeOffset as Central Time, or returns the fallback string.
    /// </summary>
    public static string FormatCentral(this DateTimeOffset? dto, string format, string fallback = "Not set")
        => dto.HasValue ? dto.Value.FormatCentral(format) : fallback;

    /// <summary>
    /// Gets today's date in Central Time.
    /// </summary>
    public static DateOnly TodayCentral
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CentralTz));

    /// <summary>
    /// Creates a DateTimeOffset from a local datetime-local input value, treating it as Central Time.
    /// </summary>
    public static DateTimeOffset? ParseAsCentral(string? value)
    {
        if (!DateTime.TryParse(value, out var dt)) return null;
        var offset = CentralTz.GetUtcOffset(dt);
        return new DateTimeOffset(dt, offset).ToUniversalTime();
    }

    /// <summary>
    /// Creates a DateTimeOffset from a date-only input value, treating it as Central Time midnight.
    /// </summary>
    public static DateTimeOffset? ParseDateAsCentral(string? value)
    {
        if (!DateOnly.TryParse(value, out var date)) return null;
        var midnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = CentralTz.GetUtcOffset(midnight);
        return new DateTimeOffset(midnight, offset).ToUniversalTime();
    }
}
