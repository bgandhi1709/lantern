using System.Reflection;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using RabbitMQ.Client;

namespace Lantern.Api.Tests.Architecture;

// CODING_STANDARDS.md, Architecture 1, 2, 3, 5 and 8.
public sealed class LayerTests
{
    public static TheoryData<string, string[]> AllowedReferences =>
        new()
        {
            { "Lantern.Core", [] },
            { "Lantern.Repository", ["Lantern.Core"] },
            { "Lantern.Base", ["Lantern.Core", "Lantern.Repository"] },
            { "Lantern.Api", ["Lantern.Core", "Lantern.Base"] },
            { "Lantern.Functions", ["Lantern.Core", "Lantern.Base"] },
        };

    [Theory]
    [MemberData(nameof(AllowedReferences))]
    public void EachLayer_ReferencesOnlyTheLayersBelowIt(string assembly, string[] allowed)
    {
        var references = LanternAssemblies.All
            .Single(candidate => candidate.GetName().Name == assembly)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Lantern.", StringComparison.Ordinal));

        Assert.Empty(references.Except(allowed));
    }

    [Fact]
    public void Entities_AreInternalToTheRepository()
    {
        var entities = LanternAssemblies.Repository.GetTypes()
            .Where(type => type.Namespace == "Lantern.Repository.Entities" && !type.Name.Contains('<', StringComparison.Ordinal));

        Assert.All(entities, entity => Assert.False(entity.IsPublic, $"{entity.Name} is public"));
    }

    // Each Azure client has exactly one owner; everything else goes through it.
    public static TheoryData<Type, string> ClientOwners =>
        new()
        {
            { typeof(TableClient), "UnitOfWork`1" },
            { typeof(TableServiceClient), "UnitOfWork`1" },
            { typeof(BlobContainerClient), "WorkspaceStore" },
            { typeof(BlobServiceClient), "WorkspaceStore" },
            { typeof(IConnection), "RabbitMqPublishingService" },
            { typeof(IChannel), "RabbitMqPublishingService" },
        };

    [Theory]
    [MemberData(nameof(ClientOwners))]
    public void AnAzureClient_IsHeldOnlyByItsOwner(Type client, string owner)
    {
        var holders = LanternAssemblies.Concrete
            .Where(type => Holds(type, client))
            .Select(type => type.Name)
            .Distinct();

        Assert.All(holders, holder => Assert.Equal(owner, holder));
    }

    [Fact]
    public void ABrokerClientType_IsMentionedOnlyByThePublishingService()
    {
        var broker = typeof(IConnection).Assembly;
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        var mentioning = LanternAssemblies.Concrete
            .Where(type =>
                type.GetFields(all).Select(field => field.FieldType)
                    .Concat(type.GetProperties(all).Select(property => property.PropertyType))
                    .Concat(type.GetConstructors(all).SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType))
                    .Concat(type.GetMethods(all).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
                    .Any(member => Mentions(member, broker)))
            .Select(type => type.Name)
            .Distinct()
            .ToList();

        Assert.Equal(["RabbitMqPublishingService"], mentioning);
    }

    [Fact]
    public void ABrokerTrigger_IsOnlyOnAFunctionClass()
    {
        var triggered = LanternAssemblies.Concrete
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetParameters().Any(parameter => parameter.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "RabbitMQTriggerAttribute")))
            .Select(method => method.DeclaringType!.Name)
            .ToList();

        Assert.NotEmpty(triggered);
        Assert.All(triggered, name => Assert.EndsWith("Function", name, StringComparison.Ordinal));
    }

    [Fact]
    public void Options_HaveASectionName_AndNoDefaults()
    {
        var options = LanternAssemblies.Concrete.Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(options);
        Assert.All(options, type =>
        {
            Assert.EndsWith(".Configuration", type.Namespace, StringComparison.Ordinal);
            Assert.NotNull(type.GetField("SectionName", BindingFlags.Public | BindingFlags.Static));

            var instance = Activator.CreateInstance(type)!;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = property.GetValue(instance);
                var empty = value is null
                    || value is string { Length: 0 }
                    || (property.PropertyType.IsValueType && value.Equals(Activator.CreateInstance(property.PropertyType)));
                Assert.True(empty, $"{type.Name}.{property.Name} has a default; set it in the bicepparam instead.");
            }
        });
    }

    private static bool Holds(Type type, Type client) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Any(field => Mentions(field.FieldType, client))
        || type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => Mentions(parameter.ParameterType, client));

    private static bool Mentions(Type candidate, Assembly assembly) =>
        candidate.Assembly == assembly || (candidate.IsGenericType && candidate.GetGenericArguments().Any(argument => Mentions(argument, assembly)));

    private static bool Mentions(Type candidate, Type client) =>
        client.IsAssignableFrom(candidate) || (candidate.IsGenericType && candidate.GetGenericArguments().Any(argument => Mentions(argument, client)));
}
