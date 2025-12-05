namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Configuration options for Azure Blob Storage.
/// </summary>
public class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    public string AccountName { get; set; } = string.Empty;
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>
    /// Generates the connection string from account name and key.
    /// </summary>
    public string ConnectionString =>
        $"DefaultEndpointsProtocol=https;AccountName={AccountName};AccountKey={AccountKey};EndpointSuffix=core.windows.net";
}

/// <summary>
/// Configuration options for Azure Image Analysis.
/// </summary>
public class AzureImageAnalysisOptions
{
    public const string SectionName = "ImageAnalysis";

    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}