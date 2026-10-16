using Lantern.Core.Models;
using Lantern.Core.Service;
using Lantern.Core.Storage;
using Lantern.Repository;
using Mapster;
using MapsterMapper;

namespace Lantern.Api.Tests.Infrastructure;

internal static class TestServices
{
    public static readonly IJsonService Json = new JsonService();

    public static readonly ContentLayout Layout = new();

    public static readonly IMapper Mapper = CreateMapper();

    public static ClassManifest ClassManifest(string text)
    {
        var manifest = Json.Deserialize<ClassManifest>(text);
        manifest.EnsureSchema(Layout.ClassManifestSchema);

        return manifest.EnsureChaptersMatchCounts();
    }

    public static FamilyManifest FamilyManifest(string text)
    {
        var manifest = Json.Deserialize<FamilyManifest>(text);
        manifest.EnsureSchema(Layout.FamilyManifestSchema);

        return manifest;
    }

    private static Mapper CreateMapper()
    {
        var config = new TypeAdapterConfig();
        config.Apply(new RepositoryProfile());

        return new Mapper(config);
    }
}
