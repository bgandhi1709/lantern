using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using Lantern.Core.Models;

namespace Lantern.Api.Tests.Infrastructure;

internal static class FamilyManifestSeed
{
    public static string Path(Guid familyId) => string.Create(CultureInfo.InvariantCulture, $"{familyId:D}/manifest.json");

    public static async Task PutAsync(
        BlobContainerClient family,
        Guid familyId,
        params (Guid ChildId, int ClassLevel, Subject[] Subjects)[] children
    ) => await PutRawAsync(family, familyId, Json("family-manifest-v1", children));

    public static async Task PutRawAsync(BlobContainerClient family, Guid familyId, string json)
    {
        await family.CreateIfNotExistsAsync();
        await family.GetBlobClient(Path(familyId)).UploadAsync(BinaryData.FromBytes(Encoding.UTF8.GetBytes(json)), overwrite: true);
    }

    public static string Json(string schema, params (Guid ChildId, int ClassLevel, Subject[] Subjects)[] children)
    {
        var entries = new JsonObject();
        foreach (var (childId, classLevel, subjects) in children)
        {
            entries[childId.ToString("D")] = new JsonObject
            {
                ["class"] = classLevel,
                ["profileVersion"] = "cbse-v1",
                ["subjects"] = new JsonArray(
                    [
                        .. subjects.Select(subject => (JsonNode)new JsonObject
                        {
                            ["subjectId"] = subject.SubjectId,
                            ["name"] = subject.Name,
                            ["icon"] = subject.Icon,
                            ["chapterCount"] = subject.ChapterCount,
                        }),
                    ]
                ),
            };
        }

        return new JsonObject { ["schema"] = schema, ["board"] = "cbse", ["children"] = entries }.ToJsonString();
    }
}
