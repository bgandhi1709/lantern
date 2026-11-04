using Lantern.Core.Identity;

namespace Lantern.Functions.Tests.Infrastructure;

internal sealed class CallerHolder : IIdentityResolver
{
    public CallerIdentity Identity { get; set; }
}
