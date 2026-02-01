using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Web.Components.Pages.Registration;

public static class RegistrationFormHelpers
{
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public static string GetPortionLabel(PortionOption option) => option switch
    {
        PortionOption.TopOnly => "Top Half Only",
        PortionOption.BottomOnly => "Bottom Half Only",
        PortionOption.TopOrBottom => "Choose Top or Bottom",
        _ => "Not Applicable"
    };

    public static int CalculateAge(DateOnly dateOfBirth, DateOnly cutoffDate)
    {
        if (dateOfBirth == default) return 0;

        var age = cutoffDate.Year - dateOfBirth.Year;
        if (cutoffDate < dateOfBirth.AddYears(age))
            age--;
        return age;
    }
}