using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using RegistrationSystem.Core.Application.Azure;

namespace RegistrationSystem.Web.Components.Pages.Registration.RegistrationForm.Steps;

public partial class Step2FileUpload
{
    [Parameter, EditorRequired] public RegistrationFormState FormState { get; set; } = null!;
    [Parameter] public EventCallback OnDataChanged { get; set; }
    [Parameter] public EventCallback OnNext { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private bool isSelectingIdFile = false;
    private bool isSelectingPhotoFile = false;
    private bool isValidatingId = false;
    private bool isValidatingPhoto = false;
    private string? idValidationError;
    private string? photoValidationError;
    private string? idValidationSuccess;
    private string? photoValidationSuccess;

    private async Task OnIdFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        isSelectingIdFile = true;
        StateHasChanged();

        idValidationError = null;
        idValidationSuccess = null;
        FormState.IdValidated = false;
        FormState.IdDocumentBytes = null;
        FormState.IdDocumentFileName = file.Name;
        FormState.IdDocumentContentType = file.ContentType;
        FormState.IdDocumentSize = file.Size;

        byte[] fileBytes;
        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
            FormState.IdDocumentBytes = fileBytes;
        }
        catch (Exception)
        {
            isSelectingIdFile = false;
            idValidationError = "Error reading file. Please try again or contact administrator.";
            StateHasChanged();
            return;
        }

        isSelectingIdFile = false;

        if (FormState.NiqabBypassApproved)
        {
            FormState.IdValidated = true;
            idValidationSuccess = "ID document accepted (niqab bypass approved).";
            StateHasChanged();
            return;
        }

        isValidatingId = true;
        StateHasChanged();

        try
        {
            using var validationStream = new MemoryStream(fileBytes);
            var result = await FileValidationService.ValidateIdDocumentAsync(
                validationStream,
                file.Name,
                file.Size,
                bypassFaceDetection: FormState.NiqabBypassApproved);

            if (result.IsValid)
            {
                FormState.IdValidated = true;
                idValidationSuccess = result.Details ?? "ID document validated successfully.";
            }
            else
            {
                FormState.IdDocumentBytes = null;
                FormState.IdDocumentFileName = null;
                idValidationError = result.ErrorSummary;
            }
        }
        catch (Exception)
        {
            FormState.IdDocumentBytes = null;
            FormState.IdDocumentFileName = null;
            idValidationError = "Error detected. Please try again or contact administrator.";
        }
        finally
        {
            isValidatingId = false;
            await OnDataChanged.InvokeAsync();
            StateHasChanged();
        }
    }

    private async Task OnPhotoFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        isSelectingPhotoFile = true;
        StateHasChanged();

        photoValidationError = null;
        photoValidationSuccess = null;
        FormState.PhotoValidated = false;
        FormState.PhotoBytes = null;
        FormState.PhotoFileName = file.Name;
        FormState.PhotoContentType = file.ContentType;
        FormState.PhotoSize = file.Size;

        if (!FormState.IdValidated && !FormState.NiqabBypassApproved)
        {
            isSelectingPhotoFile = false;
            photoValidationError = "Please upload and validate your ID document first.";
            FormState.PhotoFileName = null;
            StateHasChanged();
            return;
        }

        byte[] fileBytes;
        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
            FormState.PhotoBytes = fileBytes;
        }
        catch (Exception)
        {
            isSelectingPhotoFile = false;
            photoValidationError = "Error reading file. Please try again or contact administrator.";
            StateHasChanged();
            return;
        }

        isSelectingPhotoFile = false;

        if (FormState.NiqabBypassApproved)
        {
            FormState.PhotoValidated = true;
            photoValidationSuccess = "Photo accepted (niqab bypass approved).";
            StateHasChanged();
            return;
        }

        isValidatingPhoto = true;
        StateHasChanged();

        try
        {
            using var validationStream = new MemoryStream(fileBytes);
            var result = await FileValidationService.ValidatePhotoAsync(
                validationStream,
                file.Name,
                file.Size,
                bypassFaceDetection: FormState.NiqabBypassApproved);

            if (result.IsValid)
            {
                FormState.PhotoValidated = true;
                photoValidationSuccess = result.Details ?? "Photo validated successfully.";
            }
            else
            {
                FormState.PhotoBytes = null;
                FormState.PhotoFileName = null;
                photoValidationError = result.ErrorSummary;
            }
        }
        catch (Exception)
        {
            FormState.PhotoBytes = null;
            FormState.PhotoFileName = null;
            photoValidationError = "Error detected. Please try again or contact administrator.";
        }
        finally
        {
            isValidatingPhoto = false;
            await OnDataChanged.InvokeAsync();
            StateHasChanged();
        }
    }

    private void ClearIdFile()
    {
        FormState.ClearIdFile();
        idValidationError = null;
        idValidationSuccess = null;
        ClearPhotoFile();
    }

    private void ClearPhotoFile()
    {
        FormState.ClearPhotoFile();
        photoValidationError = null;
        photoValidationSuccess = null;
    }
}