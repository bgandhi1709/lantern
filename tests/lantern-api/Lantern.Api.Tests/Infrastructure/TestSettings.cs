using Microsoft.Extensions.Configuration;

namespace Lantern.Api.Tests.Infrastructure;

// The fixed settings every test host and harness runs with: appsettings.Testing.json, so no value is written in code.
internal static class TestSettings
{
    public const string FileName = "appsettings.Testing.json";

    public static string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, FileName);

    private static readonly IConfigurationRoot Root = new ConfigurationBuilder().AddJsonFile(FilePath, optional: false).Build();

    public static string SecurityKey => Root["Security:Key"];

    public static string WorkspaceContainer => Root["Storage:WorkspaceContainer"];
}
