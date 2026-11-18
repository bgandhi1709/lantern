using System.ComponentModel.DataAnnotations;
using Lantern.Core.Configuration;

namespace Lantern.Api.Tests.Configuration;

public sealed class CosmosOptionsTests
{
    [Theory]
    [InlineData("https://cosmos-lantern.documents.azure.com:443/", "lantern", "content", true)]
    [InlineData("", "lantern", "content", false)]
    [InlineData("https://cosmos-lantern.documents.azure.com:443/", "", "content", false)]
    [InlineData("https://cosmos-lantern.documents.azure.com:443/", "lantern", "", false)]
    public void Validate_NeedsTheEndpointTheDatabaseAndTheContentContainer(string endpoint, string database, string container, bool valid)
    {
        var options = new CosmosOptions { Endpoint = endpoint, Database = database, ContentContainer = container };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);

        Assert.Equal(valid, isValid);
    }

    [Fact]
    public void Key_IsEmptyUnlessSet_SoAzureSignsInWithTheManagedIdentity() => Assert.Equal(string.Empty, new CosmosOptions().Key);
}
