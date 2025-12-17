using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Service for generating SAS URIs for secure blob access.
/// </summary>
public class BlobSasService
{
    private readonly AzureStorageOptions _options;
    private readonly StorageSharedKeyCredential _credential;

    public BlobSasService(AzureStorageOptions options)
    {
        _options = options;
        _credential = new StorageSharedKeyCredential(_options.AccountName, _options.AccountKey);
    }

    /// <summary>
    /// Generates a time-limited SAS URI for reading a blob.
    /// </summary>
    /// <param name="containerName">The container name.</param>
    /// <param name="blobName">The blob name (storage reference).</param>
    /// <param name="expiresInMinutes">How long the URL should be valid (default 10 minutes).</param>
    /// <returns>A signed URL for downloading the blob, or null if generation fails.</returns>
    public string? GenerateReadSasUri(string containerName, string blobName, int expiresInMinutes = 10)
    {
        if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(blobName))
            return null;

        try
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b", // blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Allow for clock skew
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasToken = sasBuilder.ToSasQueryParameters(_credential);

            var uriBuilder = new UriBuilder
            {
                Scheme = "https",
                Host = $"{_options.AccountName}.blob.core.windows.net",
                Path = $"{containerName}/{blobName}",
                Query = sasToken.ToString()
            };

            return uriBuilder.Uri.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generates a SAS URI from a storage reference (blob name) by parsing the file type and division.
    /// The storage reference format is: {Type}_{DivLetter}_{FirstName}_{LastName}_{DOB}_{GUID}.{ext}
    /// Or for videos: Video_{DivLetter}_{GUID12}.{ext}
    /// </summary>
    /// <param name="storageReference">The blob name/storage reference.</param>
    /// <param name="competitionYear">The competition year for container name generation.</param>
    /// <param name="expiresInMinutes">How long the URL should be valid.</param>
    /// <returns>A signed URL for downloading the blob, or null if parsing fails.</returns>
    public string? GenerateSasUriFromReference(string storageReference, int competitionYear, int expiresInMinutes = 10)
    {
        if (string.IsNullOrWhiteSpace(storageReference))
            return null;

        try
        {
            var containerName = DetermineContainerName(storageReference, competitionYear);
            if (string.IsNullOrEmpty(containerName))
                return null;

            return GenerateReadSasUri(containerName, storageReference, expiresInMinutes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines the container name from a storage reference.
    /// Parses the file type and division from the blob name.
    /// </summary>
    private string? DetermineContainerName(string storageReference, int competitionYear)
    {
        // Format: {Type}_{DivLetter}_{...}.{ext}
        // Examples: 
        //   ID_M_John_Doe_01-15-2010_ABC1.jpeg
        //   Photo_B_Jane_Smith_03-22-2012_XYZ2.png
        //   Video_T_A3F2B1C9D4E5.mp4

        var underscoreIndex = storageReference.IndexOf('_');
        if (underscoreIndex == -1 || underscoreIndex == storageReference.Length - 1)
            return null;

        // Get file type (ID, Photo, Video)
        var fileTypeStr = storageReference[..underscoreIndex];

        // Get division letter (character after first underscore)
        var divisionLetter = storageReference[underscoreIndex + 1];

        // Map file type
        var fileType = fileTypeStr.ToUpperInvariant() switch
        {
            "ID" => "id",
            "PHOTO" => "photo",
            "VIDEO" => "video",
            _ => null
        };

        if (fileType == null)
            return null;

        // Map division letter to division name
        var division = char.ToUpperInvariant(divisionLetter) switch
        {
            'B' => "bestvoice",
            'M' => "memorization",
            'T' => "tenqiraat",
            'I' => "islamicstudies",
            _ => null
        };

        if (division == null)
            return null;

        // Build container name: {YY}-{division}-{type}
        var yearSuffix = (competitionYear % 100).ToString("D2");
        return $"{yearSuffix}-{division}-{fileType}";
    }

    /// <summary>
    /// Checks if a blob exists in storage.
    /// </summary>
    public async Task<bool> BlobExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            return await blobClient.ExistsAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a time-limited SAS URI for writing (uploading) a blob.
    /// Used for direct browser-to-Azure uploads, bypassing the server.
    /// </summary>
    /// <param name="containerName">The container name.</param>
    /// <param name="blobName">The blob name (storage reference).</param>
    /// <param name="contentType">The expected content type of the upload.</param>
    /// <param name="expiresInMinutes">How long the URL should be valid (default 30 minutes).</param>
    /// <returns>A signed URL for uploading the blob, or null if generation fails.</returns>
    public string? GenerateWriteSasUri(
        string containerName,
        string blobName,
        string contentType,
        int expiresInMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(blobName))
            return null;

        try
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b", // blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Allow for clock skew
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes),
                ContentType = contentType
            };

            // Grant Create and Write permissions for upload
            sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

            var sasToken = sasBuilder.ToSasQueryParameters(_credential);

            var uriBuilder = new UriBuilder
            {
                Scheme = "https",
                Host = $"{_options.AccountName}.blob.core.windows.net",
                Path = $"{containerName}/{blobName}",
                Query = sasToken.ToString()
            };

            return uriBuilder.Uri.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ensures a container exists, creating it if necessary.
    /// Must be called before generating a write SAS for a new container.
    /// </summary>
    public async Task EnsureContainerExistsAsync(
        string containerName,
        CancellationToken cancellationToken = default)
    {
        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(
            global::Azure.Storage.Blobs.Models.PublicAccessType.None,
            cancellationToken: cancellationToken);
    }
}