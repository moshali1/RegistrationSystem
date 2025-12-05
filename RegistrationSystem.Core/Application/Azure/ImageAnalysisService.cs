using Azure;
using Azure.AI.Vision.ImageAnalysis;

namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Service for analyzing images using Azure Image Analysis (OCR, content detection, people detection).
/// </summary>
public class ImageAnalysisService
{
    private readonly AzureImageAnalysisOptions _options;
    private readonly ImageAnalysisClient _client;

    // Keywords that indicate the image is likely an ID document
    private static readonly HashSet<string> IdKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Document types
        "passport", "license", "driver", "drivers", "driver's", "identification",
        "certificate", "birth", "id", "card", "visa", "permit", "resident",
        
        // Common fields
        "name", "surname", "first", "last", "middle", "given", "family",
        "date", "dob", "born", "birth", "expiry", "expires", "issued",
        "sex", "gender", "male", "female", "height", "weight", "eyes",
        
        // Locations
        "state", "city", "address", "country",
        "usa", "united", "states", "america", "american",
        "canada", "canadian", "mexico", "mexican",
        "minnesota", "california", "texas", "florida", "new york",
        
        // Other
        "signature", "photo", "department", "motor", "vehicle", "dmv",
        "republic", "government", "official", "federal"
    };

    public ImageAnalysisService(AzureImageAnalysisOptions options)
    {
        _options = options;
        _client = new ImageAnalysisClient(
            new Uri(_options.Endpoint),
            new AzureKeyCredential(_options.Key));
    }

    /// <summary>
    /// Detects whether an image contains an ID document by checking for ID-related keywords.
    /// </summary>
    public async Task<IdDetectionResult> DetectIdDocumentAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Read stream into BinaryData
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);
            var imageData = BinaryData.FromBytes(memoryStream.ToArray());

            // Analyze image with OCR
            var result = await _client.AnalyzeAsync(
                imageData,
                VisualFeatures.Read,
                new ImageAnalysisOptions { GenderNeutralCaption = true },
                cancellationToken);

            if (result?.Value?.Read?.Blocks == null)
            {
                return new IdDetectionResult
                {
                    IsSuccess = true,
                    IsIdDocument = false,
                    DetectedWordCount = 0,
                    MatchedKeywords = new List<string>()
                };
            }

            // Extract all words from OCR result
            var allWords = new List<string>();
            foreach (var block in result.Value.Read.Blocks)
            {
                foreach (var line in block.Lines)
                {
                    foreach (var word in line.Words)
                    {
                        allWords.Add(word.Text);
                    }
                }
            }

            // Check for ID keywords
            var matchedKeywords = allWords
                .Where(word => IdKeywords.Contains(word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Require at least 2 ID keywords to be considered an ID document
            var isIdDocument = matchedKeywords.Count >= 2;

            return new IdDetectionResult
            {
                IsSuccess = true,
                IsIdDocument = isIdDocument,
                DetectedWordCount = allWords.Count,
                MatchedKeywords = matchedKeywords,
                Confidence = CalculateIdConfidence(matchedKeywords.Count)
            };
        }
        catch (Exception ex)
        {
            return new IdDetectionResult
            {
                IsSuccess = false,
                Error = $"Failed to analyze image: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Detects people in an image using Azure Image Analysis.
    /// This doesn't require Limited Access approval unlike Face API.
    /// </summary>
    public async Task<PeopleDetectionResult> DetectPeopleAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Read stream into BinaryData
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream, cancellationToken);
            var imageData = BinaryData.FromBytes(memoryStream.ToArray());

            // Analyze image for people
            var result = await _client.AnalyzeAsync(
                imageData,
                VisualFeatures.People,
                new ImageAnalysisOptions { GenderNeutralCaption = true },
                cancellationToken);

            var peopleCount = result?.Value?.People?.Values?.Count ?? 0;

            // Filter by confidence - only count people detected with high confidence
            var confidentPeople = result?.Value?.People?.Values?
                .Where(p => p.Confidence > 0.5)
                .ToList() ?? new List<DetectedPerson>();

            return new PeopleDetectionResult
            {
                IsSuccess = true,
                PeopleCount = confidentPeople.Count,
                TotalDetections = peopleCount
            };
        }
        catch (Exception ex)
        {
            return new PeopleDetectionResult
            {
                IsSuccess = false,
                Error = $"Failed to detect people: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates that exactly one person is visible in the image.
    /// </summary>
    public async Task<SinglePersonResult> DetectSinglePersonAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        var result = await DetectPeopleAsync(imageStream, cancellationToken);

        if (!result.IsSuccess)
        {
            return new SinglePersonResult
            {
                Success = false,
                Error = result.Error
            };
        }

        if (result.PeopleCount == 0)
        {
            return new SinglePersonResult
            {
                Success = false,
                Error = "No person detected in the image. Please upload a clear photo showing yourself."
            };
        }

        if (result.PeopleCount > 1)
        {
            return new SinglePersonResult
            {
                Success = false,
                Error = "Multiple people detected. Please upload a photo with only one person."
            };
        }

        return new SinglePersonResult { Success = true };
    }

    private static double CalculateIdConfidence(int keywordCount)
    {
        return keywordCount switch
        {
            0 => 0.0,
            1 => 0.3,
            2 => 0.6,
            3 => 0.8,
            _ => 0.95
        };
    }
}

/// <summary>
/// Result of ID document detection.
/// </summary>
public class IdDetectionResult
{
    public bool IsSuccess { get; set; }
    public bool IsIdDocument { get; set; }
    public int DetectedWordCount { get; set; }
    public List<string> MatchedKeywords { get; set; } = new();
    public double Confidence { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of people detection.
/// </summary>
public class PeopleDetectionResult
{
    public bool IsSuccess { get; set; }
    public int PeopleCount { get; set; }
    public int TotalDetections { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of single person detection.
/// </summary>
public class SinglePersonResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}