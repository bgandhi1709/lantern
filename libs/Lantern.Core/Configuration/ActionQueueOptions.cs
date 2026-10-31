using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

public sealed class ActionQueueOptions
{
    public const string SectionName = ActionOptions.SectionName;

    [Required]
    public string CreateWorkspaceQueue { get; set; } = string.Empty;

    [Required]
    public string RemoveWorkspaceQueue { get; set; } = string.Empty;

    [Required]
    public string RemoveFamilyQueue { get; set; } = string.Empty;
}
