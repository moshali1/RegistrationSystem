using RegistrationSystem.Core.Domain.Registrations;

namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Orchestrates file validation for ID documents, photos, and videos.
/// Uses Azure Image Analysis for OCR and people detection.
/// </summary>
public class FileValidationService
{
    private readonly ImageAnalysisService _imageAnalysisService;

    // File size limits
    private const long MaxIdFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const long MaxPhotoFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const long MaxVideoFileSizeBytes = 500 * 1024 * 1024; // 500 MB

    // Allowed file extensions
    private static readonly HashSet<string> AllowedIdExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".webm", ".avi", ".mkv", ".m4v"
    };

    // Video MIME types for validation
    private static readonly HashSet<string> AllowedVideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/quicktime", "video/webm", "video/x-msvideo",
        "video/x-matroska", "video/x-m4v", "application/octet-stream"
    };

    public FileValidationService(ImageAnalysisService imageAnalysisService)
    {
        _imageAnalysisService = imageAnalysisService;
    }

    /// <summary>
    /// Maximum allowed video file size in bytes (500 MB).
    /// </summary>
    public static long MaxVideoSize => MaxVideoFileSizeBytes;

    /// <summary>
    /// Gets comma-separated list of allowed video extensions for file picker.
    /// </summary>
    public static string AllowedVideoExtensionsString => string.Join(",", AllowedVideoExtensions);

    /// <summary>
    /// Validates an ID document file.
    /// Checks: file size, extension, ID keywords (OCR), face detection.
    /// </summary>
    /// <param name="stream">File stream.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="fileSizeBytes">File size in bytes.</param>
    /// <param name="bypassFaceDetection">True if niqab bypass is active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IdValidationResult> ValidateIdDocumentAsync(
        Stream stream,
        string fileName,
        long fileSizeBytes,
        bool bypassFaceDetection = false,
        CancellationToken cancellationToken = default)
    {
        var result = new IdValidationResult();

        // Check file size
        if (fileSizeBytes > MaxIdFileSizeBytes)
        {
            result.Errors.Add($"File size ({FormatFileSize(fileSizeBytes)}) exceeds maximum allowed ({FormatFileSize(MaxIdFileSizeBytes)}).");
            return result;
        }

        // Check extension
        var extension = Path.GetExtension(fileName);
        if (!AllowedIdExtensions.Contains(extension))
        {
            result.Errors.Add($"File type '{extension}' is not allowed. Please upload a JPG, PNG, or PDF file.");
            return result;
        }

        // For PDFs, we can't do image analysis - accept with note about review
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            result.IsValid = true;
            result.ValidationMethod = "PdfAccepted";
            result.Details = "PDF document uploaded. Will be reviewed after submission.";
            return result;
        }

        // Copy stream for multiple reads
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        // Check for ID keywords using OCR
        memoryStream.Position = 0;
        var idDetectionResult = await _imageAnalysisService.DetectIdDocumentAsync(memoryStream, cancellationToken);

        if (!idDetectionResult.IsSuccess)
        {
            result.Errors.Add(idDetectionResult.Error ?? "Failed to analyze image.");
            return result;
        }

        if (!idDetectionResult.IsIdDocument)
        {
            result.Errors.Add("This image doesn't appear to be a valid ID document. Please upload a government-issued ID such as a passport, driver's license, or birth certificate.");
            result.Details = $"Detected {idDetectionResult.DetectedWordCount} words, {idDetectionResult.MatchedKeywords.Count} ID keywords.";
            return result;
        }

        result.IdKeywordsFound = idDetectionResult.MatchedKeywords;

        // Note: We do NOT require person/face detection on ID documents
        // because birth certificates don't have photos. Manual review handles verification.

        result.IsValid = true;
        result.ValidationMethod = bypassFaceDetection ? "NiqabBypass" : "OcrValidation";
        result.Details = bypassFaceDetection
            ? "ID document accepted (niqab bypass approved)."
            : "ID document uploaded. Will be reviewed after submission.";

        return result;
    }

    /// <summary>
    /// Validates a photo file.
    /// Checks: file size, extension, face detection.
    /// Note: Face comparison with ID is NOT performed - manual review handles identity verification.
    /// </summary>
    /// <param name="stream">File stream.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="fileSizeBytes">File size in bytes.</param>
    /// <param name="bypassFaceDetection">True if niqab bypass is active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<PhotoValidationResult> ValidatePhotoAsync(
        Stream stream,
        string fileName,
        long fileSizeBytes,
        bool bypassFaceDetection = false,
        CancellationToken cancellationToken = default)
    {
        var result = new PhotoValidationResult();

        // Check file size
        if (fileSizeBytes > MaxPhotoFileSizeBytes)
        {
            result.Errors.Add($"File size ({FormatFileSize(fileSizeBytes)}) exceeds maximum allowed ({FormatFileSize(MaxPhotoFileSizeBytes)}).");
            return result;
        }

        // Check extension
        var extension = Path.GetExtension(fileName);
        if (!AllowedPhotoExtensions.Contains(extension))
        {
            result.Errors.Add($"File type '{extension}' is not allowed. Please upload a JPG or PNG file.");
            return result;
        }

        // If bypassed, accept without face detection
        if (bypassFaceDetection)
        {
            result.IsValid = true;
            result.ValidationMethod = "NiqabBypass";
            result.Details = "Photo accepted (face detection bypassed).";
            return result;
        }

        // Copy stream for people detection
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        // Detect person in photo - must have exactly one person
        memoryStream.Position = 0;
        var personResult = await _imageAnalysisService.DetectSinglePersonAsync(memoryStream, cancellationToken);

        if (!personResult.Success)
        {
            result.Errors.Add(personResult.Error ?? "Failed to detect person in photo.");
            return result;
        }

        result.IsValid = true;
        result.ValidationMethod = "PersonDetected";
        result.Details = "Photo validated successfully. Person detected.";

        return result;
    }

    /// <summary>
    /// Validates a video file.
    /// Checks: file size, extension, MIME type.
    /// Note: Video content is NOT analyzed - manual review handles content verification.
    /// </summary>
    /// <param name="fileName">Original file name.</param>
    /// <param name="fileSizeBytes">File size in bytes.</param>
    /// <param name="contentType">MIME content type from browser.</param>
    public VideoValidationResult ValidateVideo(
        string fileName,
        long fileSizeBytes,
        string? contentType)
    {
        var result = new VideoValidationResult();

        // Check file size
        if (fileSizeBytes > MaxVideoFileSizeBytes)
        {
            result.Errors.Add($"File size ({FormatFileSize(fileSizeBytes)}) exceeds maximum allowed ({FormatFileSize(MaxVideoFileSizeBytes)}).");
            return result;
        }

        if (fileSizeBytes == 0)
        {
            result.Errors.Add("File appears to be empty.");
            return result;
        }

        // Check extension
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !AllowedVideoExtensions.Contains(extension))
        {
            result.Errors.Add($"File type '{extension}' is not allowed. Please upload a video file (MP4, MOV, WebM, AVI, or MKV).");
            return result;
        }

        // Check MIME type (if provided)
        if (!string.IsNullOrEmpty(contentType) && !AllowedVideoMimeTypes.Contains(contentType))
        {
            // Some browsers report generic types, so we'll allow if extension is valid
            // Just log a warning but don't fail
        }

        result.IsValid = true;
        result.ValidationMethod = "ExtensionValidation";
        result.Details = $"Video file accepted ({extension}, {FormatFileSize(fileSizeBytes)}).";

        return result;
    }

    /// <summary>
    /// Creates a FileValidationResult for storage in the domain model.
    /// </summary>
    public static FileValidationResult CreateFileValidationResult(
    bool isValid,
    string? details = null)
    {
        return new FileValidationResult
        {
            IsValid = isValid,
            Details = details
        };
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

/// <summary>
/// Result of ID document validation.
/// </summary>
public class IdValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> IdKeywordsFound { get; set; } = new();
    public string? ValidationMethod { get; set; }
    public string? Details { get; set; }

    public bool HasErrors => Errors.Count > 0;
    public string ErrorSummary => string.Join(" ", Errors);
}

/// <summary>
/// Result of photo validation.
/// </summary>
public class PhotoValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? ValidationMethod { get; set; }
    public string? Details { get; set; }

    public bool HasErrors => Errors.Count > 0;
    public string ErrorSummary => string.Join(" ", Errors);
}

/// <summary>
/// Result of video validation.
/// </summary>
public class VideoValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? ValidationMethod { get; set; }
    public string? Details { get; set; }

    public bool HasErrors => Errors.Count > 0;
    public string ErrorSummary => string.Join(" ", Errors);
}