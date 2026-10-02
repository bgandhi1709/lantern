using System.Security.Claims;
using Lantern.Api.Auth;
using Lantern.Core.Exceptions;
using Lantern.Core.Identity;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Lantern.Api.Tests.Auth;

public sealed class HttpIdentityResolverTests
{
    [Fact]
    public void Identity_ReadsSubNameAndEmail() =>
        Assert.Equal(
            new CallerIdentity("u1", "Meena", "m@example.test"),
            Resolver(new Claim("sub", "u1"), new Claim("name", "Meena"), new Claim("email", "m@example.test")).Identity
        );

    [Fact]
    public void Identity_WithoutNameOrEmail_UsesEmptyStrings() =>
        Assert.Equal(new CallerIdentity("u1", "", ""), Resolver(new Claim("sub", "u1")).Identity);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Identity_WithABlankOrMissingSub_Throws(string sub)
    {
        Assert.Throws<CallerNotIdentifiedException>(() => Resolver(new Claim("sub", sub)).Identity);
        Assert.Throws<CallerNotIdentifiedException>(() => Resolver().Identity);
    }

    [Fact]
    public void Identity_WithoutAnHttpContext_Throws() =>
        Assert.Throws<CallerNotIdentifiedException>(() => new HttpIdentityResolver(new Mock<IHttpContextAccessor>().Object).Identity);

    private static HttpIdentityResolver Resolver(params Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(context);

        return new HttpIdentityResolver(accessor.Object);
    }
}
