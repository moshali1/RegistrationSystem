using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.Chat;
using RegistrationSystem.Core.Domain.Registrations;

namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Service for AI-powered ID document verification. Uses OCR to extract text
/// from ID images, then Azure OpenAI to analyze the document and verify
/// identity against registration data.
/// </summary>
public class IdVerificationService
{
    private readonly ImageAnalysisService _imageAnalysisService;
    private readonly BlobStorageService _blobStorageService;
    private readonly ChatClient _chatClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    public IdVerificationService(
        ImageAnalysisService imageAnalysisService,
        BlobStorageService blobStorageService,
        ChatClient chatClient)
    {
        _imageAnalysisService = imageAnalysisService;
        _blobStorageService = blobStorageService;
        _chatClient = chatClient;
    }

    /// <summary>
    /// Verifies a registration's ID document using OCR + AI analysis.
    /// </summary>
    public async Task<IdVerificationResult> VerifyRegistrationIdAsync(
        Registration registration,
        string divisionName,
        int competitionYear,
        CancellationToken cancellationToken = default)
    {
        var result = new IdVerificationResult
        {
            RegistrationId = registration.Id,
            CompetitorName = registration.PersonalInfo.FullName,
            Cid = registration.Cid
        };

        try
        {
            // Check if ID document exists
            var idDoc = registration.FileUploadInfo?.IdDocument;
            if (idDoc == null || string.IsNullOrWhiteSpace(idDoc.StorageReference))
            {
                result.IsSkipped = true;
                result.SkipReason = "No ID document uploaded";
                return result;
            }

            // Check if it's an image (skip PDFs)
            var ext = idDoc.Extension?.TrimStart('.') ?? "";
            if (!ImageExtensions.Contains($".{ext}"))
            {
                result.IsSkipped = true;
                result.SkipReason = $"PDF/non-image document (.{ext}) — cannot process with OCR";
                return result;
            }

            // Download the ID image from blob storage
            var containerName = BlobStorageService.GenerateContainerName(
                competitionYear, divisionName, FileType.Id);

            Stream imageStream;
            try
            {
                imageStream = await _blobStorageService.DownloadAsync(
                    containerName, idDoc.StorageReference, cancellationToken);
            }
            catch (Exception ex)
            {
                result.HasError = true;
                result.ErrorMessage = $"Failed to download ID document: {ex.Message}";
                return result;
            }

            // Extract text via OCR
            OcrTextResult ocrResult;
            using (imageStream)
            {
                ocrResult = await _imageAnalysisService.ExtractAllTextAsync(
                    imageStream, cancellationToken);
            }

            if (!ocrResult.IsSuccess)
            {
                result.HasError = true;
                result.ErrorMessage = ocrResult.Error ?? "OCR extraction failed";
                return result;
            }

            if (string.IsNullOrWhiteSpace(ocrResult.ExtractedText))
            {
                result.Outcome = IdVerificationOutcome.Question;
                result.Reasoning = "No text could be extracted from the document image";
                return result;
            }

            // Send to AI for analysis
            var aiResponse = await AnalyzeWithAIAsync(
                ocrResult.ExtractedText,
                registration.PersonalInfo.FirstName,
                registration.PersonalInfo.LastName,
                cancellationToken);

            if (aiResponse == null)
            {
                result.Outcome = IdVerificationOutcome.Question;
                result.Reasoning = "AI analysis returned no response";
                return result;
            }

            // Map AI response to result
            result.DocumentType = aiResponse.DocumentType ?? "unknown";
            result.IssuingCountry = aiResponse.IssuingCountry ?? "unknown";
            result.IsAllowedRegion = aiResponse.IsAllowedRegion;
            result.ExtractedFirstName = aiResponse.ExtractedFirstName ?? "";
            result.ExtractedLastName = aiResponse.ExtractedLastName ?? "";
            result.FirstNameMatch = ParseNameMatch(aiResponse.FirstNameMatch);
            result.LastNameMatch = ParseNameMatch(aiResponse.LastNameMatch);
            result.Reasoning = aiResponse.Reasoning ?? "";
            result.Outcome = ParseOutcome(aiResponse.OverallResult);

            return result;
        }
        catch (Exception ex)
        {
            result.HasError = true;
            result.ErrorMessage = $"Verification failed: {ex.Message}";
            return result;
        }
    }

    private async Task<AiVerificationResponse?> AnalyzeWithAIAsync(
        string ocrText,
        string registeredFirstName,
        string registeredLastName,
        CancellationToken cancellationToken)
    {
        var userPrompt = $$"""
            Analyze the following OCR text extracted from an identity document. The competitor's registered name is:
            - First Name: {{registeredFirstName}}
            - Last Name: {{registeredLastName}}

            OCR Text from document:
            ---
            {{ocrText}}
            ---

            Determine:
            1. What type of document this is
            2. What country/state it was issued by
            3. Whether the country is US, Canada, or Mexico (the only allowed regions)
            4. What first and last name appear on the document
            5. Whether those names match the registered name (consider spelling variations, transliterations, nicknames)

            IMPORTANT: If the document shows additional middle names that are NOT in the registration, this is perfectly acceptable.
            Only the first name and last name need to match. A middle name on the ID that is absent from the registration should NOT affect the match result.
            For example, if registration says "John Smith" and the ID says "John Michael Smith", both firstNameMatch and lastNameMatch should be "match".

            Respond with this exact JSON structure:
            {
              "documentType": "drivers_license" or "state_id" or "passport" or "birth_certificate" or "other" or "unknown",
              "issuingCountry": "<country or state name, or 'unknown'>",
              "isAllowedRegion": true or false,
              "extractedFirstName": "<first name found on ID or empty string>",
              "extractedLastName": "<last name found on ID or empty string>",
              "firstNameMatch": "match" or "no_match" or "uncertain",
              "lastNameMatch": "match" or "no_match" or "uncertain",
              "overallResult": "pass" or "flag" or "question",
              "reasoning": "<brief explanation, max 50 words>"
            }

            Rules for overallResult:
            - "pass": Valid ID type, allowed region, both names match
            - "flag": Clearly invalid (not an ID, disallowed region, names clearly don't match)
            - "question": Uncertain on any check (unclear text, partial name match, ambiguous document type)
            """;

        try
        {
            List<ChatMessage> messages = new()
            {
                new SystemChatMessage("You are an ID document verification assistant. You analyze OCR text extracted from identity documents and determine their validity. You must respond with valid JSON only, no other text, no markdown code fences."),
                new UserChatMessage(userPrompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

            var responseText = response.Value.Content[0].Text?.Trim();
            if (string.IsNullOrWhiteSpace(responseText))
                return null;

            // Strip markdown code fences if present
            if (responseText.StartsWith("```"))
            {
                var firstNewline = responseText.IndexOf('\n');
                if (firstNewline > 0)
                    responseText = responseText[(firstNewline + 1)..];
                if (responseText.EndsWith("```"))
                    responseText = responseText[..^3].TrimEnd();
            }

            return JsonSerializer.Deserialize<AiVerificationResponse>(responseText, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception)
        {
            throw; // Let the caller handle non-JSON exceptions
        }
    }

    private static IdVerificationOutcome ParseOutcome(string? value) => value?.ToLowerInvariant() switch
    {
        "pass" => IdVerificationOutcome.Pass,
        "flag" => IdVerificationOutcome.Flag,
        _ => IdVerificationOutcome.Question
    };

    private static NameMatchResult ParseNameMatch(string? value) => value?.ToLowerInvariant() switch
    {
        "match" => NameMatchResult.Match,
        "no_match" => NameMatchResult.NoMatch,
        _ => NameMatchResult.Uncertain
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
// AI RESPONSE DTO
// ═══════════════════════════════════════════════════════════════════════════════

internal class AiVerificationResponse
{
    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("issuingCountry")]
    public string? IssuingCountry { get; set; }

    [JsonPropertyName("isAllowedRegion")]
    public bool IsAllowedRegion { get; set; }

    [JsonPropertyName("extractedFirstName")]
    public string? ExtractedFirstName { get; set; }

    [JsonPropertyName("extractedLastName")]
    public string? ExtractedLastName { get; set; }

    [JsonPropertyName("firstNameMatch")]
    public string? FirstNameMatch { get; set; }

    [JsonPropertyName("lastNameMatch")]
    public string? LastNameMatch { get; set; }

    [JsonPropertyName("overallResult")]
    public string? OverallResult { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// RESULT MODELS
// ═══════════════════════════════════════════════════════════════════════════════

public class IdVerificationResult
{
    public string RegistrationId { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public string? Cid { get; set; }

    // AI analysis results
    public IdVerificationOutcome Outcome { get; set; } = IdVerificationOutcome.Question;
    public string DocumentType { get; set; } = "unknown";
    public string IssuingCountry { get; set; } = "unknown";
    public bool IsAllowedRegion { get; set; }
    public string ExtractedFirstName { get; set; } = string.Empty;
    public string ExtractedLastName { get; set; } = string.Empty;
    public NameMatchResult FirstNameMatch { get; set; } = NameMatchResult.Uncertain;
    public NameMatchResult LastNameMatch { get; set; } = NameMatchResult.Uncertain;
    public string Reasoning { get; set; } = string.Empty;

    // Skip/Error state
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum IdVerificationOutcome
{
    Pass,
    Flag,
    Question
}

public enum NameMatchResult
{
    Match,
    NoMatch,
    Uncertain
}
