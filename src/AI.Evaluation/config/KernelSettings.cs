using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PromptEval.Config;

internal class KernelSettings
{
    public const string SectionName = "KernelSettings";

    [JsonPropertyName("serviceType")]
    [Required]
    public string ServiceType { get; set; } = string.Empty;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = string.Empty;

    [JsonPropertyName("deploymentId")]
    public string DeploymentId { get; set; } = string.Empty;

    [JsonPropertyName("modelId")]
    [Required]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = string.Empty;
}
