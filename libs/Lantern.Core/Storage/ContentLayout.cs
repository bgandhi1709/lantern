using System.Globalization;

namespace Lantern.Core.Storage;

public sealed class ContentLayout
{
    public char Separator { get; } = '/';

    public string MarkerFile { get; } = "class.json";

    public string CatalogFile { get; } = "catalog.json";

    public string ClassManifestFile { get; } = "manifest.json";

    public string FamilyManifestFile { get; } = "manifest.json";

    public string VersionFile { get; } = "version.json";

    public string BoardProfileFile { get; } = "board.json";

    public string ContentFolderName { get; } = "content";

    public string TocFolder { get; } = "toc";

    public string GuidanceFolder { get; } = "guidance";

    public string RefinedFolderName { get; } = "refined";

    public string SummariesFolderName { get; } = "summaries";

    public string MasterManifestFolder { get; } = "manifest";

    public string FileExtension { get; } = ".json";

    public string ClassManifestSchema { get; } = "class-manifest-v1";

    public string FamilyManifestSchema { get; } = "family-manifest-v1";

    public string CatalogBookField { get; } = "book_id";

    public string CatalogGradeField { get; } = "grade";

    public string FamilyFolder(Guid familyId) => string.Create(CultureInfo.InvariantCulture, $"{familyId:D}{Separator}");

    public string ChildFolder(Guid familyId, Guid childId) => string.Create(CultureInfo.InvariantCulture, $"{FamilyFolder(familyId)}{childId:D}{Separator}");

    public string MarkerPath(Guid familyId, Guid childId, int classLevel) =>
        string.Create(CultureInfo.InvariantCulture, $"{ChildFolder(familyId, childId)}{classLevel}{Separator}{MarkerFile}");

    public string ContentFolder(Guid familyId, Guid childId, int classLevel) =>
        string.Create(CultureInfo.InvariantCulture, $"{ChildFolder(familyId, childId)}{classLevel}{Separator}{ContentFolderName}{Separator}");

    public string FamilyManifestPath(Guid familyId) => FamilyFolder(familyId) + FamilyManifestFile;

    public string MasterClassManifest(int classLevel) =>
        string.Create(CultureInfo.InvariantCulture, $"{MasterManifestFolder}{Separator}class-{classLevel}{FileExtension}");

    public string RefinedFolder(string book) => $"{RefinedFolderName}{Separator}{book}{Separator}";

    public string SummariesFolder(string book) => $"{SummariesFolderName}{Separator}{book}{Separator}";

    public string SummaryOf(string refinedName) => $"{SummariesFolderName}{Separator}{refinedName[(RefinedFolderName.Length + 1)..]}";

    public string BookFile(string folder, string book) => $"{folder}{Separator}{book}{FileExtension}";
}
