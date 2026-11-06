using System.ComponentModel.DataAnnotations;
using Lantern.Core.Configuration;

namespace Lantern.Api.Tests.Configuration;

public sealed class ActionQueueOptionsTests
{
    [Theory]
    [InlineData("create-workspace", "remove-workspace", "remove-family", "parking", 143, true)]
    [InlineData("", "remove-workspace", "remove-family", "parking", 143, false)]
    [InlineData("create-workspace", "", "remove-family", "parking", 143, false)]
    [InlineData("create-workspace", "remove-workspace", "", "parking", 143, false)]
    [InlineData("create-workspace", "remove-workspace", "remove-family", "", 143, false)]
    [InlineData("create-workspace", "remove-workspace", "remove-family", "parking", 0, false)]
    public void Validate_NeedsAQueueNameForEverySubscriberTheParkingQueueAndARetryLimit(
        string create,
        string removeWorkspace,
        string removeFamily,
        string parking,
        int retryLimit,
        bool valid
    )
    {
        var options = new ActionQueueOptions
        {
            CreateWorkspaceQueue = create,
            RemoveWorkspaceQueue = removeWorkspace,
            RemoveFamilyQueue = removeFamily,
            ParkingQueue = parking,
            RetryLimit = retryLimit,
        };

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true);

        Assert.Equal(valid, isValid);
    }
}
