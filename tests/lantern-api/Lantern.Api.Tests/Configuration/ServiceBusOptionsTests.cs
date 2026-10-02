using System.ComponentModel.DataAnnotations;
using Lantern.Core.Configuration;

namespace Lantern.Api.Tests.Configuration;

public sealed class ServiceBusOptionsTests
{
    [Theory]
    [InlineData("", "", false)]
    [InlineData("Endpoint=sb://local/;", "", true)]
    [InlineData("", "sb-lantern-uat.servicebus.windows.net", true)]
    [InlineData("Endpoint=sb://local/;", "sb-lantern-uat.servicebus.windows.net", false)]
    public void Validate_NeedsExactlyOneWayToSignIn(string connectionString, string fullyQualifiedNamespace, bool valid)
    {
        var options = new ServiceBusOptions { ConnectionString = connectionString, FullyQualifiedNamespace = fullyQualifiedNamespace };

        Assert.Equal(valid, !options.Validate(new ValidationContext(options)).Any());
    }
}
