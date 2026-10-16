using System.Text.Json;
using Lantern.Api.Exceptions;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lantern.Api.Tests.Exceptions;

public sealed class ReadProblemCodesTests
{
    [Theory]
    [InlineData(LanternErrorCode.ContentNotReady, 409, "content-not-ready")]
    [InlineData(LanternErrorCode.InvalidPage, 400, "invalid-page")]
    public async Task TryHandleAsync_ACodeTheReadEndpointsThrow_IsAProblemWithItsWireCode(LanternErrorCode error, int status, string code)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await new ProblemExceptionHandler(NullLogger<ProblemExceptionHandler>.Instance).TryHandleAsync(
            context,
            new LanternException(error),
            CancellationToken.None
        );

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
