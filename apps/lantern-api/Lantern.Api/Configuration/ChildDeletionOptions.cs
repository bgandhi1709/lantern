using System.ComponentModel.DataAnnotations;

namespace Lantern.Api.Configuration;

public sealed class ChildDeletionOptions
{
    public const string SectionName = "Deletion";

    [Range(1, 3600)]
    public int PollSeconds { get; set; } = 15;
}
