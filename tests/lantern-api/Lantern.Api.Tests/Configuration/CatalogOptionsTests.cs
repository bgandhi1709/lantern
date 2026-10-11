using System.ComponentModel.DataAnnotations;
using Lantern.Core.Configuration;

namespace Lantern.Api.Tests.Configuration;

public sealed class CatalogOptionsTests
{
    [Theory]
    [InlineData("ncert", "ssc", true)]
    [InlineData("", "ssc", false)]
    [InlineData("ncert", "", false)]
    [InlineData("", "", false)]
    public void Validate_NeedsAContainerNameForEveryBoard(string ncert, string ssc, bool valid)
    {
        var options = new CatalogOptions { NcertContainer = ncert, SscContainer = ssc };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);

        Assert.Equal(valid, isValid);
    }
}
