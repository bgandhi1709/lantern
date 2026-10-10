using Lantern.Core.Constants;
using Lantern.Core.Exceptions;

namespace Lantern.Api.Tests.Infrastructure;

// Asserts a LanternException with the given code, so a test names the failure and not just its type.
internal static class Errors
{
    public static void Throws(LanternErrorCode code, Action action) =>
        Assert.Equal(code, Assert.Throws<LanternException>(action).Code);

    public static async Task ThrowsAsync(LanternErrorCode code, Func<Task> action) =>
        Assert.Equal(code, (await Assert.ThrowsAsync<LanternException>(action)).Code);
}
