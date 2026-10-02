extern alias Functions;

using System.Text.RegularExpressions;
using Lantern.Core.Actions;
using Lantern.Core.Service;
using Microsoft.AspNetCore.Mvc;
using IActionHandler = Functions::Lantern.Functions.Handler.IActionHandler;

namespace Lantern.Api.Tests.Architecture;

// CODING_STANDARDS.md, Architecture 7, and the code glossary in docs/architecture/code-glossary.md.
public sealed partial class NamingTests
{
    private static readonly string[] ProductionFolders = ["apps", "libs"];

    private static readonly string[] BannedSuffixes = ["Dto", "Helper", "Helpers", "Manager", "Utils", "Util", "View", "Record", "Body"];

    [Fact]
    public void NoType_UsesABannedSuffix() =>
        Assert.All(
            LanternAssemblies.Types,
            type => Assert.DoesNotContain(BannedSuffixes, suffix => type.Name.Split('`')[0].EndsWith(suffix, StringComparison.Ordinal))
        );

    [Fact]
    public void Interfaces_StartWithI() =>
        Assert.All(LanternAssemblies.Types.Where(type => type.IsInterface), type => Assert.Matches("^I[A-Z]", type.Name));

    [Fact]
    public void Services_EndWithService_AndHaveTheirOwnInterface()
    {
        var services = LanternAssemblies.Concrete
            .Where(type => type.Assembly == LanternAssemblies.Base && Implements(type, typeof(IServiceBase<>)));

        Assert.NotEmpty(services);
        Assert.All(services, type =>
        {
            Assert.EndsWith("Service", type.Name, StringComparison.Ordinal);
            Assert.Contains(type.GetInterfaces(), contract => contract.Name == $"I{type.Name}");
        });
    }

    [Fact]
    public void Repositories_AreNamedAfterTheirModel_AndTheirEntityToo()
    {
        var repositories = LanternAssemblies.Concrete.Where(type => BaseRepositoryArguments(type) is not null).ToList();

        Assert.NotEmpty(repositories);
        Assert.All(repositories, type =>
        {
            var arguments = BaseRepositoryArguments(type)!;
            Assert.Equal($"{arguments[0].Name}Repository", type.Name);
            Assert.Equal($"{arguments[0].Name}Entity", arguments[1].Name);
            Assert.Contains(type.GetInterfaces(), contract => contract.Name == $"I{type.Name}");
        });
    }

    [Fact]
    public void Stores_EndWithStore() =>
        Assert.All(
            LanternAssemblies.Concrete.Where(type => type.GetInterfaces().Any(contract => contract.Name.EndsWith("Store", StringComparison.Ordinal))),
            type => Assert.EndsWith("Store", type.Name, StringComparison.Ordinal)
        );

    [Fact]
    public void Validators_AreNamedAfterTheModelTheyValidate()
    {
        var validators = LanternAssemblies.Concrete
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IValidator<>))
                .Select(contract => (type, model: contract.GetGenericArguments()[0])))
            .ToList();

        Assert.NotEmpty(validators);
        Assert.All(validators, pair => Assert.Equal($"{pair.model.Name}Validator", pair.type.Name));
    }

    [Fact]
    public void EveryActionType_HasOneHandlerAndOnePayloadNamedAfterIt()
    {
        var handlers = LanternAssemblies.Concrete.Where(type => typeof(IActionHandler).IsAssignableFrom(type)).Select(type => type.Name).ToList();
        var payloads = LanternAssemblies.Core.GetTypes().Select(type => type.Name).ToHashSet();

        Assert.All(Enum.GetNames<ActionType>(), name =>
        {
            Assert.Contains($"{name}Handler", handlers);
            Assert.Contains($"{name}Payload", payloads);
        });
        Assert.Equal(Enum.GetNames<ActionType>().Length, handlers.Count);
    }

    [Fact]
    public void Controllers_AreVersionedAndEndWithController() =>
        Assert.All(
            LanternAssemblies.Concrete.Where(type => typeof(ControllerBase).IsAssignableFrom(type)),
            type =>
            {
                Assert.EndsWith("Controller", type.Name, StringComparison.Ordinal);
                Assert.Matches(@"^Lantern\.Api\.Controllers\.V\d+$", type.Namespace!);
            }
        );

    [Fact]
    public void ApiModels_LiveInLanternApiModelsOnly() =>
        Assert.All(
            LanternAssemblies.Types.Where(type => type.Name.EndsWith("Model", StringComparison.Ordinal) && !type.IsInterface),
            type => Assert.Equal("Lantern.Api.Models", type.Namespace)
        );

    // Source-level: one top-level type per file, named after it. Generated and test code is excluded.
    [Fact]
    public void EachSourceFile_DeclaresOneTypeNamedAfterIt()
    {
        var root = LanternAssemblies.RepositoryRoot.FullName;
        var files = ProductionFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories));

        var mismatches = files
            .Select(file => (file, names: TopLevelType().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value).ToHashSet()))
            .Where(entry => entry.names.Count > 0 && !entry.names.SetEquals([Path.GetFileNameWithoutExtension(entry.file)]))
            .Select(entry => $"{Path.GetRelativePath(root, entry.file)}: {string.Join(", ", entry.names)}");

        Assert.Empty(mismatches);
    }

    private static bool Implements(Type type, Type openInterface) =>
        type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openInterface);

    private static Type[]? BaseRepositoryArguments(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition().Name == "BaseRepository`2")
            {
                return current.GetGenericArguments();
            }
        }

        return null;
    }

    [GeneratedRegex(@"^(?:\[[^\]]*\]\s*)*(?:(?:public|internal|file|sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+struct)?|interface|enum|struct)\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();
}
