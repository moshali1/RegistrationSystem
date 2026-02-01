namespace RegistrationSystem.Web.Components.Pages.Registration;

public static class RegistrationFormatter
{
    public static string FormatName(string? name)
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

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length == 11 && digits.StartsWith("1"))
            digits = digits[1..];

        return digits.Length == 10;
    }

    public static string FormatPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length == 11 && digits.StartsWith("1"))
            digits = digits[1..];

        if (digits.Length == 10)
            return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";

        return digits;
    }
}
