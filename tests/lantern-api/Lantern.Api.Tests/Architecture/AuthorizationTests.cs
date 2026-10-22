using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Tests.Architecture;

public sealed class AuthorizationTests
{
    [Fact]
    public void EveryController_DeclaresItsOwnAuthorizeOrAllowAnonymous()
    {
        var controllers = LanternAssemblies.Api.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type)).ToList();
        Assert.NotEmpty(controllers);

        var undeclared = controllers
            .Where(type => !type.IsDefined(typeof(AuthorizeAttribute), inherit: false) && !type.IsDefined(typeof(AllowAnonymousAttribute), inherit: false))
            .Select(type => type.Name);

        Assert.Empty(undeclared);
    }

    [Fact]
    public void NoController_IsAnonymous()
    {
        var anonymous = LanternAssemblies
            .Api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && type.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
            .Select(type => type.Name);

        Assert.Empty(anonymous);
    }
}
