using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Service for uploading and managing files in Azure Blob Storage.
/// </summary>
public class BlobStorageService
{
    private readonly AzureStorageOptions _options;
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(AzureStorageOptions options)
    {
        _options = options;
        _blobServiceClient = new BlobServiceClient(_options.ConnectionString);
    }

    /// <summary>
    /// Uploads a file to Azure Blob Storage.
    /// </summary>
    /// <param name="stream">File stream to upload.</param>
    /// <param name="containerName">Target container name.</param>
    /// <param name="blobName">Target blob name (file name in storage).</param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full URI of the uploaded blob.</returns>
    public async Task<string> UploadAsync(
        Stream stream,
        string containerName,
        string blobName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Create container if it doesn't exist
        await containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        stream.Position = 0;
        await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Uploads a large file to Azure Blob Storage with progress tracking.
    /// Uses streaming to avoid loading the entire file into memory.
    /// Suitable for video files up to 500 MB.
    /// </summary>
    /// <param name="stream">File stream to upload (will be read from current position).</param>
    /// <param name="totalSize">Total file size in bytes (for progress calculation).</param>
    /// <param name="containerName">Target container name.</param>
    /// <param name="blobName">Target blob name (file name in storage).</param>
    /// <param name="contentType">MIME content type.</param>
    /// <param name="progress">Progress reporter (reports bytes uploaded).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full URI of the uploaded blob.</returns>
    public async Task<string> UploadWithProgressAsync(
        Stream stream,
        long totalSize,
        string containerName,
        string blobName,
        string contentType,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Create container if it doesn't exist
        await containerClient.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);

        // Configure upload options for large files
        // Azure SDK handles chunking automatically with these settings
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            },
            TransferOptions = new global::Azure.Storage.StorageTransferOptions
            {
                // Use 4 MB chunks for optimal performance
                InitialTransferSize = 4 * 1024 * 1024,
                MaximumTransferSize = 4 * 1024 * 1024,
                MaximumConcurrency = 4
            },
            ProgressHandler = progress != null
                ? new Progress<long>(bytesTransferred => progress.Report(bytesTransferred))
                : null
        };

        await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Downloads a file from Azure Blob Storage.
    /// </summary>
    public async Task<Stream> DownloadAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var response = await blobClient.DownloadAsync(cancellationToken);
        return response.Value.Content;
    }

    /// <summary>
    /// Deletes a file from Azure Blob Storage.
    /// </summary>
    public async Task DeleteAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Checks if a blob exists.
    /// </summary>
    public async Task<bool> ExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync(cancellationToken);
    }

    /// <summary>
    /// Generates a container name based on competition year and division.
    /// Format: {YY}-{division-lowercase}-{type}
    /// Example: 26-memorization-id
    /// </summary>
    public static string GenerateContainerName(int competitionYear, string divisionName, FileType fileType)
    {
        var yearSuffix = (competitionYear % 100).ToString("D2");
        var divisionLower = NormalizeName(divisionName).ToLowerInvariant();
        var typeLower = fileType.ToString().ToLowerInvariant();

        return $"{yearSuffix}-{divisionLower}-{typeLower}";
    }

    /// <summary>
    /// Generates a blob name (file name) based on file type and competitor info.
    /// Format: {Type}_{DivisionLetter}_{FirstName}_{LastName}_{DOB}_{GUID4}.{ext}
    /// Example: ID_M_Abdirahman_Abdiaziz_11-02-2011_CD67.jpeg
    /// </summary>
    public static string GenerateBlobName(
        FileType fileType,
        string divisionName,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        string extension)
    {
        var type = fileType.ToString();
        var divisionLetter = GetDivisionLetter(divisionName);
        var firstNameClean = NormalizeName(firstName);
        var lastNameClean = NormalizeName(lastName);
        var dobFormatted = dateOfBirth.ToString("MM-dd-yyyy");
        var guidSuffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var ext = extension.TrimStart('.');

        return $"{type}_{divisionLetter}_{firstNameClean}_{lastNameClean}_{dobFormatted}_{guidSuffix}.{ext}";
    }

    /// <summary>
    /// Generates a blob name for video files using GUID-based naming.
    /// This avoids issues when personal info changes after upload.
    /// Format: Video_{DivisionLetter}_{GUID12}.{ext}
    /// Example: Video_M_A3F2B1C9D4E5.mp4
    /// </summary>
    public static string GenerateVideoBlobName(string divisionName, string extension)
    {
        var divisionLetter = GetDivisionLetter(divisionName);
        var guid = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var ext = extension.TrimStart('.');

        return $"Video_{divisionLetter}_{guid}.{ext}";
    }

    /// <summary>
    /// Gets the first letter of the division name (uppercase).
    /// </summary>
    private static string GetDivisionLetter(string divisionName)
    {
        if (string.IsNullOrWhiteSpace(divisionName))
            return "X";

        return divisionName.Trim()[0].ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Normalizes a name for use in file/container names.
    /// Removes special characters, trims, and handles spaces.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unknown";

        // Remove special characters, keep letters, numbers, and hyphens
        var normalized = new string(name
            .Trim()
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ')
            .ToArray());

        // Replace spaces with nothing (or you could use hyphens)
        normalized = normalized.Replace(" ", "");

        return string.IsNullOrEmpty(normalized) ? "Unknown" : normalized;
    }
}

/// <summary>
/// Types of files that can be uploaded.
/// </summary>
public enum FileType
{
    Id,
    Photo,
    Video
}