using Microsoft.Extensions.Logging;

namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Service for handling direct video uploads to Azure Blob Storage.
/// Generates SAS URLs that allow browser-to-Azure direct uploads.
/// </summary>
public class VideoUploadService
{
    private readonly BlobSasService _sasService;
    private readonly AzureStorageOptions _options;
    private readonly ILogger<VideoUploadService> _logger;

    public VideoUploadService(
        BlobSasService sasService,
        AzureStorageOptions options,
        ILogger<VideoUploadService> logger)
    {
        _sasService = sasService;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Prepares a direct upload by generating SAS URL and ensuring container exists.
    /// </summary>
    /// <param name="competitionYear">The competition year.</param>
    /// <param name="divisionName">The division name.</param>
    /// <param name="fileName">Original file name (for extension).</param>
    /// <param name="contentType">The video content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload preparation result with SAS URL and blob info.</returns>
    public async Task<VideoUploadPrepareResult> PrepareUploadAsync(
        int competitionYear,
        string divisionName,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate container and blob names
            var containerName = BlobStorageService.GenerateContainerName(competitionYear, divisionName, FileType.Video);
            var blobName = BlobStorageService.GenerateVideoBlobName(divisionName, Path.GetExtension(fileName));

            // Ensure container exists (required before SAS URL will work)
            await _sasService.EnsureContainerExistsAsync(containerName, cancellationToken);

            // Generate SAS URL with write permissions (valid for 30 minutes)
            var sasUrl = _sasService.GenerateWriteSasUri(containerName, blobName, contentType, expiresInMinutes: 30);

            if (string.IsNullOrEmpty(sasUrl))
            {
                return VideoUploadPrepareResult.Failure("Failed to generate upload URL");
            }

            // Generate the final blob URI (without SAS token) for storage
            var blobUri = $"https://{_options.AccountName}.blob.core.windows.net/{containerName}/{blobName}";

            _logger.LogInformation(
                "Prepared video upload: Container={Container}, Blob={Blob}",
                containerName,
                blobName);

            return VideoUploadPrepareResult.Success(sasUrl, blobUri, blobName, containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare video upload");
            return VideoUploadPrepareResult.Failure("Failed to prepare upload: " + ex.Message);
        }
    }

    /// <summary>
    /// Verifies that a blob was successfully uploaded.
    /// </summary>
    public async Task<bool> VerifyUploadAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        return await _sasService.BlobExistsAsync(containerName, blobName, cancellationToken);
    }
}

/// <summary>
/// Result of preparing a video upload.
/// </summary>
public class VideoUploadPrepareResult
{
    public bool IsSuccess { get; private set; }
    public string? Error { get; private set; }
    public string? SasUrl { get; private set; }
    public string? BlobUri { get; private set; }
    public string? BlobName { get; private set; }
    public string? ContainerName { get; private set; }

    public static VideoUploadPrepareResult Success(
        string sasUrl,
        string blobUri,
        string blobName,
        string containerName)
    {
        return new VideoUploadPrepareResult
        {
            IsSuccess = true,
            SasUrl = sasUrl,
            BlobUri = blobUri,
            BlobName = blobName,
            ContainerName = containerName
        };
    }

    public static VideoUploadPrepareResult Failure(string error)
    {
        return new VideoUploadPrepareResult
        {
            IsSuccess = false,
            Error = error
        };
    }
}