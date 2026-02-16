namespace RegistrationSystem.Core.Application.Azure;

/// <summary>
/// Configuration options for Azure OpenAI.
/// </summary>
public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4.1-mini";
}
