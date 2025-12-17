using Microsoft.JSInterop;
using System.Text.Json;

namespace RegistrationSystem.Web.Services;

/// <summary>
/// Service for persisting form drafts to browser localStorage.
/// Helps recover user input after Blazor Server SignalR disconnections.
/// </summary>
public class FormDraftService
{
    private readonly IJSRuntime _js;
    private readonly ILogger<FormDraftService> _logger;

    private const string DraftKeyPrefix = "registration-draft-";
    private const string TempFilesKeyPrefix = "registration-tempfiles-";

    public FormDraftService(IJSRuntime js, ILogger<FormDraftService> logger)
    {
        _js = js;
        _logger = logger;
    }

    /// <summary>
    /// Saves form draft to localStorage
    /// </summary>
    public async Task SaveDraftAsync<T>(string userId, T formData) where T : class
    {
        try
        {
            var key = $"{DraftKeyPrefix}{userId}";
            var json = JsonSerializer.Serialize(formData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await _js.InvokeVoidAsync("localStorage.setItem", key, json);
            _logger.LogDebug("Saved draft for user {UserId}", userId);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected, can't save - this is expected during disconnection
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save draft for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Restores form draft from localStorage
    /// </summary>
    public async Task<T?> RestoreDraftAsync<T>(string userId) where T : class
    {
        try
        {
            var key = $"{DraftKeyPrefix}{userId}";
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);

            if (string.IsNullOrEmpty(json))
                return null;

            var draft = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogDebug("Restored draft for user {UserId}", userId);
            return draft;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore draft for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Checks if a draft exists without loading it
    /// </summary>
    public async Task<bool> HasDraftAsync(string userId)
    {
        try
        {
            var key = $"{DraftKeyPrefix}{userId}";
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);
            return !string.IsNullOrEmpty(json);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clears the draft after successful submission
    /// </summary>
    public async Task ClearDraftAsync(string userId)
    {
        try
        {
            var key = $"{DraftKeyPrefix}{userId}";
            await _js.InvokeVoidAsync("localStorage.removeItem", key);

            // Also clear temp file references
            var tempKey = $"{TempFilesKeyPrefix}{userId}";
            await _js.InvokeVoidAsync("localStorage.removeItem", tempKey);

            _logger.LogDebug("Cleared draft for user {UserId}", userId);
        }
        catch (JSDisconnectedException)
        {
            // Expected during disconnection
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear draft for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Saves temp file references (blob URIs for files already uploaded to temp storage)
    /// </summary>
    public async Task SaveTempFileReferencesAsync(string userId, TempFileReferences refs)
    {
        try
        {
            var key = $"{TempFilesKeyPrefix}{userId}";
            var json = JsonSerializer.Serialize(refs);
            await _js.InvokeVoidAsync("localStorage.setItem", key, json);
        }
        catch (JSDisconnectedException)
        {
            // Expected during disconnection
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save temp file references for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Restores temp file references
    /// </summary>
    public async Task<TempFileReferences?> RestoreTempFileReferencesAsync(string userId)
    {
        try
        {
            var key = $"{TempFilesKeyPrefix}{userId}";
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);

            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<TempFileReferences>(json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// References to files that have been uploaded to temporary storage.
/// These survive disconnections because they're already server-side.
/// </summary>
public class TempFileReferences
{
    public TempFileInfo? IdDocument { get; set; }
    public TempFileInfo? Photo { get; set; }
    public TempFileInfo? Video { get; set; }
}

public class TempFileInfo
{
    public string? BlobName { get; set; }
    public string? BlobUri { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}