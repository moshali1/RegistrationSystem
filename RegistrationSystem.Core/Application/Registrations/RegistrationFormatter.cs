using RegistrationSystem.Core.Domain.Registrations;
using System.Text.RegularExpressions;

namespace RegistrationSystem.Core.Application.Registrations;

public static partial class RegistrationFormatter
{
    public static void Format(Registration registration)
    {
        registration.PersonalInfo.FirstName = FormatName(registration.PersonalInfo.FirstName);
        registration.PersonalInfo.MiddleName = FormatName(registration.PersonalInfo.MiddleName);
        registration.PersonalInfo.LastName = FormatName(registration.PersonalInfo.LastName);
        registration.PersonalInfo.PreferredName = FormatName(registration.PersonalInfo.PreferredName);
        registration.PersonalInfo.PhoneNumber = FormatPhoneNumber(registration.PersonalInfo.PhoneNumber);

        registration.AddressInfo.City = FormatName(registration.AddressInfo.City);

        registration.ParentInfo.FirstName = FormatName(registration.ParentInfo.FirstName);
        registration.ParentInfo.LastName = FormatName(registration.ParentInfo.LastName);
        registration.ParentInfo.PhoneNumber = FormatPhoneNumber(registration.ParentInfo.PhoneNumber);

        if (registration.TeacherInfo != null)
        {
            registration.TeacherInfo.FirstName = FormatName(registration.TeacherInfo.FirstName);
            registration.TeacherInfo.LastName = FormatName(registration.TeacherInfo.LastName);
            registration.TeacherInfo.PhoneNumber = FormatPhoneNumber(registration.TeacherInfo.PhoneNumber);
            registration.TeacherInfo.Institution = FormatName(registration.TeacherInfo.Institution);
        }
    }

    private static string FormatName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var formatted = words.Select(word =>
        {
            if (word.Length == 1)
                return char.ToUpper(word[0]).ToString();

            return char.ToUpper(word[0]) + word[1..].ToLower();
        });

        return string.Join(" ", formatted);
    }

    public static bool IsValidPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return true;

        var digits = PhoneDigitsRegex().Replace(phone, "");

        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        return digits.Length == 10;
    }

    public static string FormatPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digits = PhoneDigitsRegex().Replace(phone, "");

        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        if (digits.Length == 10)
            return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";

        return digits;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex PhoneDigitsRegex();
}
