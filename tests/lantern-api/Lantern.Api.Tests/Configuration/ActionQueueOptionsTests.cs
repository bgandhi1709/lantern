using System.ComponentModel.DataAnnotations;
using Lantern.Core.Configuration;

namespace Lantern.Api.Tests.Configuration;

public sealed class ActionQueueOptionsTests
{
    [Theory]
    [InlineData("create-workspace", "remove-workspace", "remove-family", true)]
    [InlineData("", "remove-workspace", "remove-family", false)]
    [InlineData("create-workspace", "", "remove-family", false)]
    [InlineData("create-workspace", "remove-workspace", "", false)]
    public void Validate_NeedsAQueueNameForEverySubscriber(string create, string removeWorkspace, string removeFamily, bool valid)
    {
        var options = new ActionQueueOptions
        {
            CreateWorkspaceQueue = create,
            RemoveWorkspaceQueue = removeWorkspace,
            RemoveFamilyQueue = removeFamily,
        };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);

        Assert.Equal(valid, isValid);
    }
}
