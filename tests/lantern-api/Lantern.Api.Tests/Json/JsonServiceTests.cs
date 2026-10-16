using System.Text.Json;
using Lantern.Core.Actions;
using Lantern.Core.Constants;
using Lantern.Api.Tests.Infrastructure;
using Lantern.Core.Service;

namespace Lantern.Api.Tests.Json;

public sealed class JsonServiceTests
{
    private sealed class Sample
    {
        public string FamilyName { get; set; }

        public int ClassLevel { get; set; }
    }

    private readonly IJsonService json = TestServices.Json;

    [Fact]
    public void Serialize_WritesCamelCaseNames_OnOneLine() =>
        Assert.Equal("""{"familyName":"Rao","classLevel":3}""", json.Serialize(new Sample { FamilyName = "Rao", ClassLevel = 3 }));

    [Fact]
    public void Deserialize_ReadsNamesWhateverTheirCase()
    {
        var sample = json.Deserialize<Sample>("""{ "FAMILYNAME": "Rao", "classlevel": 3 }""");

        Assert.Equal("Rao", sample.FamilyName);
        Assert.Equal(3, sample.ClassLevel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Deserialize_NullOrEmptyText_ReturnsNull(string text) => Assert.Null(json.Deserialize<Sample>(text));

    [Fact]
    public void Deserialize_TextThatIsNotJson_Throws() => Assert.ThrowsAny<JsonException>(() => json.Deserialize<Sample>("{ not json"));

    [Fact]
    public void SerializeIndented_UsesSeveralLines_AndKeepsNonAsciiTextReadable()
    {
        var text = json.SerializeIndented(new Sample { FamilyName = "गणित", ClassLevel = 3 });

        Assert.Contains(Environment.NewLine, text, StringComparison.Ordinal);
        Assert.Contains("गणित", text, StringComparison.Ordinal);
        Assert.Equal("गणित", json.Deserialize<Sample>(text).FamilyName);
    }

    // The queue contract (ADR-0004): a message sent by one release is read by the next, so these bytes must not drift.
    [Fact]
    public void Serialize_AnActionMessage_IsThePinnedWireFormat() =>
        Assert.Equal(
            """{"id":"a1","type":"create-workspace","payload":"{}","traceParent":null}""",
            json.Serialize(new ActionMessage("a1", ActionType.CreateWorkspace, "{}"))
        );
}
