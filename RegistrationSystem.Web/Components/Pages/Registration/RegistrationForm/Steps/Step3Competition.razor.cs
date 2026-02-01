using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RegistrationSystem.Core.Application.Azure;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Web.Components.Pages.Registration.Steps;

public partial class Step3Competition : IAsyncDisposable
{
    [Inject] private VideoUploadService VideoUploadService { get; set; } = null!;
    [Inject] private FileValidationService FileValidationService { get; set; } = null!;  // ADD THIS LINE
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired] public RegistrationFormState FormState { get; set; } = null!;
    [Parameter] public CompetitionSettings? Settings { get; set; }
    [Parameter] public IReadOnlyList<Division> AvailableDivisions { get; set; } = Array.Empty<Division>();
    [Parameter] public IReadOnlyList<Category> AvailableCategories { get; set; } = Array.Empty<Category>();
    [Parameter] public EventCallback<string> OnDivisionChanged { get; set; }
    [Parameter] public EventCallback<string> OnCategoryChanged { get; set; }
    [Parameter] public EventCallback OnDataChanged { get; set; }
    [Parameter] public EventCallback OnNext { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private Category? selectedCategory;
    private bool isUploadingVideo = false;
    private string? videoValidationError;
    private int videoUploadProgress = 0;
    private string? currentUploadId;
    private string? pendingBlobUri;
    private DotNetObjectReference<Step3Competition>? uploadDotNetRef;
    private bool videoInputInitialized = false;

    protected override void OnParametersSet()
    {
        selectedCategory = GetSelectedCategory();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (selectedCategory?.RequiresVideo == true && !FormState.VideoUploaded && !videoInputInitialized)
        {
            await InitializeVideoInput();
        }
    }

    private async Task InitializeVideoInput()
    {
        try
        {
            uploadDotNetRef?.Dispose();
            uploadDotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("directUpload.initializeInput", "video-upload-input", uploadDotNetRef);
            videoInputInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize video input: {ex.Message}");
        }
    }

    private Category? GetSelectedCategory()
    {
        if (string.IsNullOrEmpty(FormState.CategoryId))
            return null;

        var division = AvailableDivisions.FirstOrDefault(d => d.Id == FormState.DivisionId);
        return division?.FindCategory(FormState.CategoryId);
    }

    private async Task HandleDivisionChanged(ChangeEventArgs e)
    {
        var divisionId = e.Value?.ToString() ?? string.Empty;
        videoInputInitialized = false;
        await OnDivisionChanged.InvokeAsync(divisionId);
    }

    private async Task HandleCategoryChanged(ChangeEventArgs e)
    {
        var categoryId = e.Value?.ToString() ?? string.Empty;
        videoInputInitialized = false;
        await OnCategoryChanged.InvokeAsync(categoryId);
        StateHasChanged();
    }

    [JSInvokable]
    public async Task OnFileSelected(VideoFileInfo fileInfo)
    {
        videoValidationError = null;
        videoUploadProgress = 0;
        FormState.VideoValidated = false;
        FormState.VideoUploaded = false;
        FormState.VideoBlobUri = null;
        FormState.VideoBlobName = null;

        if (fileInfo == null)
        {
            videoValidationError = "No file selected";
            await InvokeAsync(StateHasChanged);
            return;
        }

        FormState.VideoFileName = fileInfo.Name;
        FormState.VideoContentType = string.IsNullOrEmpty(fileInfo.Type) ? "video/mp4" : fileInfo.Type;
        FormState.VideoSize = fileInfo.Size;

        var validationResult = FileValidationService.ValidateVideo(fileInfo.Name, fileInfo.Size, FormState.VideoContentType);
        if (!validationResult.IsValid)
        {
            videoValidationError = validationResult.ErrorSummary;
            FormState.VideoFileName = null;
            await JS.InvokeVoidAsync("directUpload.clearInput", "video-upload-input");
            await InvokeAsync(StateHasChanged);
            return;
        }

        FormState.VideoValidated = true;

        var selectedDivision = AvailableDivisions.FirstOrDefault(d => d.Id == FormState.DivisionId);
        var divisionName = selectedDivision?.Name ?? "unknown";

        var prepareResult = await VideoUploadService.PrepareUploadAsync(
            Settings?.CompetitionInfo?.CompetitionYear ?? DateTime.UtcNow.Year,
            divisionName,
            fileInfo.Name,
            FormState.VideoContentType);

        if (!prepareResult.IsSuccess)
        {
            videoValidationError = prepareResult.Error ?? "Failed to prepare upload";
            FormState.VideoFileName = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        currentUploadId = Guid.NewGuid().ToString();
        FormState.VideoBlobName = prepareResult.BlobName;
        pendingBlobUri = prepareResult.BlobUri;

        isUploadingVideo = true;
        await InvokeAsync(StateHasChanged);

        await JS.InvokeVoidAsync(
            "directUpload.startUpload",
            currentUploadId,
            prepareResult.SasUrl,
            FormState.VideoContentType,
            prepareResult.BlobUri,
            prepareResult.BlobName);
    }

    [JSInvokable]
    public async Task OnUploadProgress(int percent, long uploaded, long total)
    {
        videoUploadProgress = percent;
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnUploadComplete(bool success, string message)
    {
        isUploadingVideo = false;

        if (success)
        {
            FormState.VideoUploaded = true;
            FormState.VideoBlobUri = pendingBlobUri;
            videoUploadProgress = 100;
        }
        else
        {
            ClearVideoFile(clearError: false);
            videoValidationError = string.IsNullOrEmpty(message) ? "Upload failed" : message;
        }

        await OnDataChanged.InvokeAsync();
        await InvokeAsync(StateHasChanged);

        currentUploadId = null;
        pendingBlobUri = null;
    }

    [JSInvokable]
    public async Task OnUploadError(string error)
    {
        isUploadingVideo = false;
        ClearVideoFile(clearError: false);
        videoValidationError = error;
        await InvokeAsync(StateHasChanged);

        currentUploadId = null;
        pendingBlobUri = null;
    }

    [JSInvokable]
    public async Task OnUploadCancelled()
    {
        isUploadingVideo = false;
        ClearVideoFile(clearError: false);
        videoValidationError = "Upload was cancelled.";
        await InvokeAsync(StateHasChanged);

        currentUploadId = null;
        pendingBlobUri = null;
    }

    private async Task CancelVideoUpload()
    {
        if (!string.IsNullOrEmpty(currentUploadId))
        {
            await JS.InvokeVoidAsync("directUpload.cancelUpload", currentUploadId);
        }
    }

    private void ClearVideoFile(bool clearError = true)
    {
        FormState.ClearVideoFile();
        videoUploadProgress = 0;

        if (clearError)
        {
            videoValidationError = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        uploadDotNetRef?.Dispose();
        try
        {
            await JS.InvokeVoidAsync("directUpload.dispose");
        }
        catch { }
    }

    public class VideoFileInfo
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Type { get; set; } = string.Empty;
        public long LastModified { get; set; }
    }
}