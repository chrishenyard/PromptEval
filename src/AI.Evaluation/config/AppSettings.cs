using System.ComponentModel.DataAnnotations;

namespace PromptEval.config;

internal class AppSettings
{
    public const string SectionName = "AppSettings";

    [Required]
    public string ChatType { get; set; } = null!;
}
