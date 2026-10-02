using Lantern.Repository;

namespace Lantern.Api.Tests.Storage;

// These names are part of every stored cipher's authenticated data: changing one makes existing rows unreadable.
public sealed class RowKeyServiceTests
{
    private readonly RowKeyService _keyService = new();

    [Fact]
    public void Keys_AreFixedInShape()
    {
        var id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", _keyService.FamilyPartition(id));
        Assert.Equal("child_0f8fad5bd9cb469fa16570867728950e", _keyService.ChildRowKey(id));
        Assert.Equal("parent_abc", _keyService.MembershipRowKey("abc"));
        Assert.Equal(("family", "profile", "child_"), (_keyService.FamilyRowKey, _keyService.ProfileRowKey, _keyService.ChildRowPrefix));
    }
}
