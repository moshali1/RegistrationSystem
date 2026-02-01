using RegistrationSystem.Core.Application.Registrations;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Web.Components.Pages.Registration;

public class RegistrationFormValidator
{
    private readonly RegistrationService _registrationService;

    public RegistrationFormValidator(RegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    public List<string> ValidateStep1(RegistrationFormState state)
    {
        var errors = new List<string>();

        ValidateName(state.FirstName, "First name", minLength: 2, required: true, errors);
        ValidateName(state.MiddleName, "Middle name", minLength: 1, required: false, errors);
        ValidateName(state.LastName, "Last name", minLength: 2, required: true, errors);
        ValidateName(state.PreferredName, "Preferred name", minLength: 1, required: false, errors);

        if (state.DateOfBirth == default)
            errors.Add("Date of birth is required.");

        if (!string.IsNullOrWhiteSpace(state.CompetitorPhone) &&
            !RegistrationFormatter.IsValidPhoneNumber(state.CompetitorPhone))
            errors.Add("Please enter a valid 10-digit phone number.");

        return errors;
    }

    public List<string> ValidateStep2(RegistrationFormState state)
    {
        var errors = new List<string>();

        if (!state.IdValidated)
            errors.Add("Please upload and validate your ID document.");
        if (!state.PhotoValidated)
            errors.Add("Please upload and validate your photo.");

        return errors;
    }

    public List<string> ValidateStep3(RegistrationFormState state, Category? category, bool isUploadingVideo)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(state.DivisionId))
            errors.Add("Please select a division.");
        if (string.IsNullOrWhiteSpace(state.CategoryId))
            errors.Add("Please select a category.");

        if (category?.PortionOption == PortionOption.TopOrBottom && state.PortionChoice == null)
            errors.Add("Please select a portion (Top or Bottom).");

        if (category?.RequiresVideo == true)
        {
            if (isUploadingVideo)
                errors.Add("Please wait for the video upload to complete.");
            else if (!state.VideoUploaded)
                errors.Add("Please upload a video for this category.");
        }

        return errors;
    }

    public List<string> ValidateStep4(RegistrationFormState state)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(state.Country))
            errors.Add("Country is required.");
        if (string.IsNullOrWhiteSpace(state.StateProvince))
            errors.Add("State/Province is required.");
        if (string.IsNullOrWhiteSpace(state.City))
            errors.Add("City is required.");

        ValidateName(state.ParentFirstName, "Parent/Guardian first name", minLength: 2, required: true, errors);
        ValidateName(state.ParentLastName, "Parent/Guardian last name", minLength: 2, required: true, errors);

        if (string.IsNullOrWhiteSpace(state.ParentPhone))
            errors.Add("Parent/Guardian phone number is required.");
        else if (!RegistrationFormatter.IsValidPhoneNumber(state.ParentPhone))
            errors.Add("Please enter a valid 10-digit phone number for Parent/Guardian.");

        ValidateName(state.TeacherFirstName, "Teacher first name", minLength: 2, required: true, errors);
        ValidateName(state.TeacherLastName, "Teacher last name", minLength: 2, required: true, errors);

        if (string.IsNullOrWhiteSpace(state.TeacherPhone))
            errors.Add("Teacher phone number is required.");
        else if (!RegistrationFormatter.IsValidPhoneNumber(state.TeacherPhone))
            errors.Add("Please enter a valid 10-digit phone number for Teacher.");

        if (string.IsNullOrWhiteSpace(state.TeacherInstitution))
            errors.Add("Institution name is required.");
        else if (state.TeacherInstitution.Trim().Length < 2)
            errors.Add("Institution name must be at least 2 characters.");

        return errors;
    }

    private static void ValidateName(string? value, string fieldName, int minLength, bool required, List<string> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (required && string.IsNullOrWhiteSpace(trimmed))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            if (trimmed.Length < minLength)
                errors.Add($"{fieldName} must be at least {minLength} character{(minLength > 1 ? "s" : "")}.");

            if (trimmed.Any(char.IsDigit))
                errors.Add($"{fieldName} cannot contain numbers.");
        }
    }
}