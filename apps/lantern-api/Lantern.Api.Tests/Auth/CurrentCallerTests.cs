using System.Security.Claims;
using Lantern.Api.Auth;
using Lantern.Api.Exceptions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Lantern.Api.Tests.Auth;

public sealed class CurrentCallerTests
{
    [Fact]
    public void Require_ReadsSubNameAndEmail()
    {
        var caller = Current(new Claim("sub", "u1"), new Claim("name", "Meena"), new Claim("email", "m@example.test")).Require();

        Assert.Equal(new Caller("u1", "Meena", "m@example.test"), caller);
    }

    [Fact]
    public void Require_WithoutNameOrEmail_UsesEmptyStrings() =>
        Assert.Equal(new Caller("u1", "", ""), Current(new Claim("sub", "u1")).Require());

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Require_WithABlankOrMissingSub_Throws(string sub)
    {
        Assert.Throws<CallerNotIdentifiedException>(() => Current(new Claim("sub", sub)).Require());
        Assert.Throws<CallerNotIdentifiedException>(() => Current().Require());
    }

    [Fact]
    public void Require_WithoutAnHttpContext_Throws()
    {
        var accessor = new Mock<IHttpContextAccessor>();

        Assert.Throws<CallerNotIdentifiedException>(() => new CurrentCaller(accessor.Object).Require());
    }

    private static CurrentCaller Current(params Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(context);

        return new CurrentCaller(accessor.Object);
    }
}
