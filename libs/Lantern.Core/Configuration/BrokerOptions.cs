using System.ComponentModel.DataAnnotations;

namespace Lantern.Core.Configuration;

public sealed class BrokerOptions
{
    public const string SectionName = "Broker";

    [Required]
    public string Url { get; set; } = string.Empty;
}
