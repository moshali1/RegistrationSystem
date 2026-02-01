using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RegistrationSystem.Core.Application.Auditing;
using RegistrationSystem.Core.Application.Azure;
using RegistrationSystem.Core.Application.NiqabBypasses;
using RegistrationSystem.Core.Application.Registrations;
using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Domain.Auditing;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Settings;
using RegistrationSystem.Core.ReferenceData;
using RegistrationSystem.Web.Services;

namespace RegistrationSystem.Web.Components.Pages.Registration.RegistrationForm;

public partial class RegistrationForm : IAsyncDisposable
{
    [Inject] private RegistrationService RegistrationService { get; set; } = null!;
    [Inject] private SettingsService SettingsService { get; set; } = null!;
    [Inject] private CurrentUserService CurrentUserService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private FileValidationService FileValidationService { get; set; } = null!;
    [Inject] private BlobStorageService BlobStorageService { get; set; } = null!;
    [Inject] private NiqabBypassService NiqabBypassService { get; set; } = null!;
    [Inject] private IAuditService AuditService { get; set; } = null!;
    [Inject] private BlazorAuditContextProvider AuditContextProvider { get; set; } = null!;
    [Inject] private FormDraftService FormDraftService { get; set; } = null!;
    [Inject] private VideoUploadService VideoUploadService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private bool isLoading = true;
    private bool isSubmitting;
    private int currentStep = 1;
    private const int totalSteps = 5;

    private CompetitionSettings? settings;
    private int competitionYear;
    private string currentUserId = string.Empty;
    private string currentUserType = string.Empty;
    private List<string> validationErrors = new();

    private RegistrationFormState formState = new();
    private RegistrationFormValidator? validator;

    private IReadOnlyList<Division> availableDivisions = Array.Empty<Division>();
    private IReadOnlyList<Category> availableCategories = Array.Empty<Category>();

    private Country? selectedCountry;
    private IReadOnlyList<StateProvince> availableStates = Array.Empty<StateProvince>();

    private bool showDraftRecoveryBanner = false;
    private bool draftFilesNeedReupload = false;
    private bool isSavingDraft = false;
    private bool showDraftSavedIndicator = false;
    private Timer? draftSaveTimer;
    private bool draftRestoreAttempted = false;
    private bool submittedSuccessfully = false;
    private const int DraftSaveDebounceMs = 2000;

    protected override async Task OnInitializedAsync()
    {
        isLoading = true;

        try
        {
            var user = await CurrentUserService.GetCurrentUserAsync();
            if (user == null)
            {
                NavigationManager.NavigateTo("/registrations");
                return;
            }

            currentUserId = user.ObjectIdentifier;
            currentUserType = user.UserType;
            settings = await SettingsService.GetSettingsAsync();
            competitionYear = settings?.CompetitionInfo?.CompetitionYear ?? DateTime.UtcNow.Year;

            validator = new RegistrationFormValidator(RegistrationService);
            availableDivisions = await RegistrationService.GetAvailableDivisionsAsync();
        }
        finally
        {
            isLoading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !draftRestoreAttempted && !string.IsNullOrEmpty(currentUserId))
        {
            draftRestoreAttempted = true;
            await TryRestoreDraftAsync();
        }
    }

    private void GoToStep(int step)
    {
        if (step >= 1 && step <= totalSteps)
        {
            validationErrors.Clear();
            currentStep = step;
            ScheduleDraftSave();
        }
    }

    private async Task HandleNext()
    {
        validationErrors.Clear();

        if (!ValidateCurrentStep())
            return;

        if (currentStep == 1)
        {
            await CheckNiqabBypass();
        }

        if (currentStep == 4)
        {
            FormatFormData();
        }

        if (currentStep < totalSteps)
        {
            currentStep++;
            ScheduleDraftSave();
        }
    }

    private void HandleBack()
    {
        if (currentStep > 1)
        {
            validationErrors.Clear();
            currentStep--;
        }
    }

    private bool ValidateCurrentStep()
    {
        if (validator == null) return false;

        validationErrors = currentStep switch
        {
            1 => validator.ValidateStep1(formState),
            2 => validator.ValidateStep2(formState),
            3 => validator.ValidateStep3(formState, GetSelectedCategory(), false),
            4 => validator.ValidateStep4(formState),
            _ => new List<string>()
        };

        return validationErrors.Count == 0;
    }

    private Division? GetSelectedDivision()
    {
        if (string.IsNullOrEmpty(formState.DivisionId))
            return null;
        return availableDivisions.FirstOrDefault(d => d.Id == formState.DivisionId)
            ?? settings?.FindDivision(formState.DivisionId);
    }

    private Category? GetSelectedCategory()
    {
        var division = GetSelectedDivision();
        if (division == null || string.IsNullOrEmpty(formState.CategoryId))
            return null;
        return division.FindCategory(formState.CategoryId);
    }

    private async Task HandleDivisionChanged(string divisionId)
    {
        formState.DivisionId = divisionId;
        formState.CategoryId = string.Empty;
        formState.PortionChoice = null;
        formState.ClearVideoFile();

        await LoadCategoriesAsync();
        ScheduleDraftSave();
    }

    private void HandleCategoryChanged(string categoryId)
    {
        formState.CategoryId = categoryId;
        formState.PortionChoice = null;
        formState.ClearVideoFile();
        ScheduleDraftSave();
    }

    private async Task LoadCategoriesAsync()
    {
        if (string.IsNullOrEmpty(formState.DivisionId))
        {
            availableCategories = Array.Empty<Category>();
            return;
        }

        availableCategories = await RegistrationService.GetAvailableCategoriesAsync(
            formState.DivisionId,
            formState.DateOfBirth != default ? formState.DateOfBirth : null);
    }

    private void HandleCountryChanged(string countryName)
    {
        formState.Country = countryName;
        formState.StateProvince = string.Empty;

        selectedCountry = !string.IsNullOrEmpty(countryName)
            ? LocationData.GetCountryByName(countryName)
            : null;

        availableStates = selectedCountry?.StatesProvinces ?? Array.Empty<StateProvince>();
        ScheduleDraftSave();
    }

    private void HandleStateChanged(string stateName)
    {
        formState.StateProvince = stateName;
        ScheduleDraftSave();
    }

    private void HandleDataChanged()
    {
        ScheduleDraftSave();
    }

    private void FormatFormData()
    {
        formState.FirstName = RegistrationFormatter.FormatName(formState.FirstName);
        formState.MiddleName = RegistrationFormatter.FormatName(formState.MiddleName);
        formState.LastName = RegistrationFormatter.FormatName(formState.LastName);
        formState.PreferredName = RegistrationFormatter.FormatName(formState.PreferredName);

        formState.ParentFirstName = RegistrationFormatter.FormatName(formState.ParentFirstName);
        formState.ParentLastName = RegistrationFormatter.FormatName(formState.ParentLastName);

        formState.TeacherFirstName = RegistrationFormatter.FormatName(formState.TeacherFirstName);
        formState.TeacherLastName = RegistrationFormatter.FormatName(formState.TeacherLastName);
        formState.TeacherInstitution = formState.TeacherInstitution?.Trim() ?? string.Empty;

        formState.City = RegistrationFormatter.FormatName(formState.City);

        formState.CompetitorPhone = RegistrationFormatter.FormatPhoneNumber(formState.CompetitorPhone);
        formState.ParentPhone = RegistrationFormatter.FormatPhoneNumber(formState.ParentPhone);
        formState.TeacherPhone = RegistrationFormatter.FormatPhoneNumber(formState.TeacherPhone);
    }

    private async Task CheckNiqabBypass()
    {
        if (formState.Gender != Gender.Female) return;
        if (string.IsNullOrWhiteSpace(formState.FirstName) ||
            string.IsNullOrWhiteSpace(formState.LastName) ||
            formState.DateOfBirth == default) return;

        try
        {
            var bypass = await NiqabBypassService.FindValidBypassAsync(
                formState.FirstName,
                formState.LastName,
                formState.DateOfBirth,
                competitionYear);

            if (bypass != null)
            {
                formState.NiqabBypassApproved = true;
                formState.NiqabBypassCode = bypass.Code;
            }
        }
        catch
        {
            // Silently ignore
        }
    }

    private async Task SubmitRegistration()
    {
        if (isSubmitting) return;

        validationErrors.Clear();

        if (!formState.TermsAccepted)
        {
            validationErrors.Add("You must accept the terms and conditions.");
            return;
        }

        if (!formState.NiqabBypassApproved)
        {
            if (!formState.IdValidated || !formState.HasIdDocument)
            {
                validationErrors.Add("Please upload and validate your ID document.");
                return;
            }
            if (!formState.PhotoValidated || !formState.HasPhoto)
            {
                validationErrors.Add("Please upload and validate your photo.");
                return;
            }
        }

        var selectedCategory = GetSelectedCategory();
        if (selectedCategory?.RequiresVideo == true)
        {
            if (!formState.VideoUploaded || string.IsNullOrEmpty(formState.VideoBlobUri))
            {
                validationErrors.Add("Please upload a video for this category.");
                return;
            }
        }

        if (currentUserType.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            var (isValid, error) = await RegistrationService.ValidateStudentIdentityAsync(
                currentUserId,
                formState.FirstName,
                formState.LastName,
                formState.DateOfBirth);

            if (!isValid)
            {
                validationErrors.Add(error!);
                return;
            }
        }

        isSubmitting = true;
        try
        {
            var division = GetSelectedDivision();
            var divisionName = division?.Name ?? "Unknown";

            FileUploadInfo? fileUploadInfo = null;

            if (formState.HasIdDocument && formState.HasPhoto)
            {
                var idContainerName = BlobStorageService.GenerateContainerName(
                    competitionYear, divisionName, FileType.Id);
                var photoContainerName = BlobStorageService.GenerateContainerName(
                    competitionYear, divisionName, FileType.Photo);

                var idBlobName = BlobStorageService.GenerateBlobName(
                    FileType.Id,
                    divisionName,
                    formState.FirstName,
                    formState.LastName,
                    formState.DateOfBirth,
                    Path.GetExtension(formState.IdDocumentFileName ?? ".jpg"));

                var photoBlobName = BlobStorageService.GenerateBlobName(
                    FileType.Photo,
                    divisionName,
                    formState.FirstName,
                    formState.LastName,
                    formState.DateOfBirth,
                    Path.GetExtension(formState.PhotoFileName ?? ".jpg"));

                using var idStream = new MemoryStream(formState.IdDocumentBytes!);
                await BlobStorageService.UploadAsync(
                    idStream, idContainerName, idBlobName, formState.IdDocumentContentType ?? "image/jpeg");

                using var photoStream = new MemoryStream(formState.PhotoBytes!);
                await BlobStorageService.UploadAsync(
                    photoStream, photoContainerName, photoBlobName, formState.PhotoContentType ?? "image/jpeg");

                fileUploadInfo = new FileUploadInfo
                {
                    IdDocument = new FileMetadata
                    {
                        FileName = formState.IdDocumentFileName ?? "id-document",
                        StorageReference = idBlobName,
                        FileSizeBytes = formState.IdDocumentSize,
                        Extension = Path.GetExtension(formState.IdDocumentFileName ?? ".jpg"),
                        ContentType = formState.IdDocumentContentType ?? "image/jpeg",
                        ValidationResult = FileValidationService.CreateFileValidationResult(
                            true,
                            formState.NiqabBypassApproved
                                ? "ID document accepted (niqab bypass)"
                                : "ID document validated")
                    },
                    Photo = new FileMetadata
                    {
                        FileName = formState.PhotoFileName ?? "photo",
                        StorageReference = photoBlobName,
                        FileSizeBytes = formState.PhotoSize,
                        Extension = Path.GetExtension(formState.PhotoFileName ?? ".jpg"),
                        ContentType = formState.PhotoContentType ?? "image/jpeg",
                        ValidationResult = FileValidationService.CreateFileValidationResult(
                            true,
                            formState.NiqabBypassApproved
                                ? "Photo accepted (niqab bypass)"
                                : "Photo validated - person detected")
                    },
                    Video = formState.VideoUploaded && !string.IsNullOrEmpty(formState.VideoBlobName)
                        ? new FileMetadata
                        {
                            FileName = formState.VideoFileName ?? "video",
                            StorageReference = formState.VideoBlobName,
                            FileSizeBytes = formState.VideoSize,
                            Extension = Path.GetExtension(formState.VideoFileName ?? ".mp4"),
                            ContentType = formState.VideoContentType ?? "video/mp4",
                            ValidationResult = FileValidationService.CreateFileValidationResult(
                                true,
                                $"Video uploaded ({RegistrationFormHelpers.FormatFileSize(formState.VideoSize)})")
                        }
                        : null,
                    NiqabBypassApproved = formState.NiqabBypassApproved,
                    NiqabBypassCode = formState.NiqabBypassCode
                };
            }

            var registration = new Core.Domain.Registrations.Registration
            {
                CreatorUserId = currentUserId,
                CompetitionYear = competitionYear,
                Status = RegistrationStatus.AwaitingReview,
                TermsAccepted = true,
                PersonalInfo = new PersonalInfo
                {
                    FirstName = formState.FirstName,
                    MiddleName = formState.MiddleName,
                    LastName = formState.LastName,
                    PreferredName = formState.PreferredName,
                    Gender = formState.Gender,
                    DateOfBirth = formState.DateOfBirth,
                    PhoneNumber = formState.CompetitorPhone
                },
                AddressInfo = new AddressInfo
                {
                    Country = formState.Country,
                    StateProvince = formState.StateProvince,
                    City = formState.City
                },
                CompetitionSelection = new CompetitionSelection
                {
                    DivisionId = formState.DivisionId,
                    CategoryId = formState.CategoryId,
                    PortionChoice = formState.PortionChoice
                },
                ParentInfo = new ParentInfo
                {
                    FirstName = formState.ParentFirstName,
                    LastName = formState.ParentLastName,
                    PhoneNumber = formState.ParentPhone
                },
                TeacherInfo = new TeacherInfo
                {
                    FirstName = formState.TeacherFirstName,
                    LastName = formState.TeacherLastName,
                    PhoneNumber = formState.TeacherPhone,
                    Institution = formState.TeacherInstitution
                },
                FileUploadInfo = fileUploadInfo
            };

            var result = await RegistrationService.CreateAndSubmitAsync(registration);

            if (result.IsValid)
            {
                try
                {
                    await AuditContextProvider.SetCurrentUserContextAsync();
                    var categoryName = selectedCategory?.Name ?? "Unknown Category";

                    await AuditService.LogAsync(
                        AuditAction.Submitted,
                        "Registration",
                        registration.Id,
                        summary: "New registration submitted",
                        entityDescription: $"{formState.FirstName} {formState.LastName} - {divisionName} / {categoryName}");
                }
                catch { }

                await FormDraftService.ClearDraftAsync(currentUserId);
                submittedSuccessfully = true;
                draftSaveTimer?.Dispose();
                NavigationManager.NavigateTo("/registrations");
                await JS.InvokeVoidAsync("directUpload.clearState");
            }
            else
            {
                validationErrors.AddRange(result.Errors);
            }
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private void ScheduleDraftSave()
    {
        draftSaveTimer?.Dispose();
        draftSaveTimer = new Timer(async _ =>
        {
            await InvokeAsync(async () => await SaveDraftAsync());
        }, null, DraftSaveDebounceMs, Timeout.Infinite);
    }

    private async Task SaveDraftAsync()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;

        try
        {
            isSavingDraft = true;
            StateHasChanged();

            var draft = CreateDraftFromFormData();
            await FormDraftService.SaveDraftAsync(currentUserId, draft);

            isSavingDraft = false;
            showDraftSavedIndicator = true;
            StateHasChanged();

            await Task.Delay(2000);
            showDraftSavedIndicator = false;
            StateHasChanged();
        }
        catch
        {
            isSavingDraft = false;
            StateHasChanged();
        }
    }

    private RegistrationFormDraft CreateDraftFromFormData()
    {
        return new RegistrationFormDraft
        {
            FirstName = formState.FirstName,
            MiddleName = formState.MiddleName,
            LastName = formState.LastName,
            PreferredName = formState.PreferredName,
            Gender = (int)formState.Gender,
            DateOfBirth = formState.DateOfBirth != default
                ? formState.DateOfBirth.ToString("O")
                : null,
            CompetitorPhone = formState.CompetitorPhone,

            Country = formState.Country,
            StateProvince = formState.StateProvince,
            City = formState.City,

            DivisionId = formState.DivisionId,
            CategoryId = formState.CategoryId,
            PortionChoice = formState.PortionChoice.HasValue ? (int)formState.PortionChoice.Value : null,

            ParentFirstName = formState.ParentFirstName,
            ParentLastName = formState.ParentLastName,
            ParentPhone = formState.ParentPhone,

            TeacherFirstName = formState.TeacherFirstName,
            TeacherLastName = formState.TeacherLastName,
            TeacherPhone = formState.TeacherPhone,
            TeacherInstitution = formState.TeacherInstitution,

            NiqabBypassApproved = formState.NiqabBypassApproved,
            NiqabBypassCode = formState.NiqabBypassCode,

            IdDocumentFileName = formState.IdDocumentFileName,
            IdDocumentContentType = formState.IdDocumentContentType,
            IdDocumentSize = formState.IdDocumentSize,
            IdValidated = formState.IdValidated,

            PhotoFileName = formState.PhotoFileName,
            PhotoContentType = formState.PhotoContentType,
            PhotoSize = formState.PhotoSize,
            PhotoValidated = formState.PhotoValidated,

            VideoBlobName = formState.VideoBlobName,
            VideoBlobUri = formState.VideoBlobUri,
            VideoFileName = formState.VideoFileName,
            VideoContentType = formState.VideoContentType,
            VideoSize = formState.VideoSize,
            VideoValidated = formState.VideoValidated,
            VideoUploaded = formState.VideoUploaded,

            CurrentStep = currentStep,
            SavedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task TryRestoreDraftAsync()
    {
        try
        {
            var draft = await FormDraftService.RestoreDraftAsync<RegistrationFormDraft>(currentUserId);

            if (draft == null || draft.IsStale(maxAgeHours: 24))
            {
                if (draft != null)
                    await FormDraftService.ClearDraftAsync(currentUserId);
                return;
            }

            RestoreFormDataFromDraft(draft);

            draftFilesNeedReupload = string.IsNullOrEmpty(draft.IdDocumentTempBlobName) ||
                                     string.IsNullOrEmpty(draft.PhotoTempBlobName);

            showDraftRecoveryBanner = true;

            if (!string.IsNullOrEmpty(formState.DivisionId))
            {
                await LoadCategoriesAsync();
            }

            if (!string.IsNullOrEmpty(formState.Country))
            {
                selectedCountry = LocationData.GetCountryByName(formState.Country);
                availableStates = selectedCountry?.StatesProvinces ?? Array.Empty<StateProvince>();
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Draft restore failed: {ex.Message}");
        }
    }

    private void RestoreFormDataFromDraft(RegistrationFormDraft draft)
    {
        formState.FirstName = draft.FirstName;
        formState.MiddleName = draft.MiddleName;
        formState.LastName = draft.LastName;
        formState.PreferredName = draft.PreferredName;
        formState.Gender = (Gender)draft.Gender;
        formState.DateOfBirth = !string.IsNullOrEmpty(draft.DateOfBirth)
            ? DateOnly.Parse(draft.DateOfBirth)
            : default;
        formState.CompetitorPhone = draft.CompetitorPhone;

        formState.Country = draft.Country;
        formState.StateProvince = draft.StateProvince;
        formState.City = draft.City;

        formState.DivisionId = draft.DivisionId;
        formState.CategoryId = draft.CategoryId;
        formState.PortionChoice = draft.PortionChoice.HasValue ? (PortionChoice)draft.PortionChoice.Value : null;

        formState.ParentFirstName = draft.ParentFirstName;
        formState.ParentLastName = draft.ParentLastName;
        formState.ParentPhone = draft.ParentPhone;

        formState.TeacherFirstName = draft.TeacherFirstName;
        formState.TeacherLastName = draft.TeacherLastName;
        formState.TeacherPhone = draft.TeacherPhone;
        formState.TeacherInstitution = draft.TeacherInstitution;

        formState.NiqabBypassApproved = draft.NiqabBypassApproved;
        formState.NiqabBypassCode = draft.NiqabBypassCode;

        formState.IdDocumentFileName = draft.IdDocumentFileName;
        formState.IdDocumentContentType = draft.IdDocumentContentType;
        formState.IdDocumentSize = draft.IdDocumentSize;
        formState.IdValidated = false;

        formState.PhotoFileName = draft.PhotoFileName;
        formState.PhotoContentType = draft.PhotoContentType;
        formState.PhotoSize = draft.PhotoSize;
        formState.PhotoValidated = false;

        formState.VideoBlobName = draft.VideoBlobName;
        formState.VideoBlobUri = draft.VideoBlobUri;
        formState.VideoFileName = draft.VideoFileName;
        formState.VideoContentType = draft.VideoContentType;
        formState.VideoSize = draft.VideoSize;
        formState.VideoValidated = draft.VideoValidated;
        formState.VideoUploaded = draft.VideoUploaded;

        currentStep = draft.CurrentStep;
    }

    private void DismissDraftBanner()
    {
        showDraftRecoveryBanner = false;
    }

    private async Task StartFresh()
    {
        try
        {
            await FormDraftService.ClearDraftAsync(currentUserId);
        }
        catch { }

        formState = new RegistrationFormState();
        currentStep = 1;
        validationErrors.Clear();

        availableCategories = Array.Empty<Category>();
        selectedCountry = null;
        availableStates = Array.Empty<StateProvince>();

        showDraftRecoveryBanner = false;
        draftFilesNeedReupload = false;
    }

    public async ValueTask DisposeAsync()
    {
        draftSaveTimer?.Dispose();

        if (submittedSuccessfully) return;

        if (!string.IsNullOrEmpty(currentUserId) &&
            (!string.IsNullOrEmpty(formState.FirstName) || !string.IsNullOrEmpty(formState.LastName)))
        {
            try
            {
                var draft = CreateDraftFromFormData();
                await FormDraftService.SaveDraftAsync(currentUserId, draft);
            }
            catch { }
        }
    }
}
