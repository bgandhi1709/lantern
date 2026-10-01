using Lantern.Api.Repository;

namespace Lantern.Api.Tests.Repository;

public sealed class RowKeysTests
{
    private readonly RowKeys keys = new();

    [Fact]
    public void Keys_AreFixedInShape()
    {
        var id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", keys.FamilyPartition(id));
        Assert.Equal("child_0f8fad5bd9cb469fa16570867728950e", keys.ChildRowKey(id));
    }

    [Fact]
    public void IsChildRow_OnlyForChildRows()
    {
        Assert.True(keys.IsChildRow(keys.ChildRowKey(Guid.NewGuid())));
        Assert.False(keys.IsChildRow("family"));
        Assert.False(keys.IsChildRow("parent_abc"));
    }
}
