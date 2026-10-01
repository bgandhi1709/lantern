using Asp.Versioning;
using Lantern.Api.Auth;
using Lantern.Api.Configuration;
using Lantern.Api.Contracts;
using Lantern.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lantern.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("v1/family/children")]
[Authorize]
[EnableRateLimiting(RateLimits.Children)]
[RequestSizeLimit(MaxBodyBytes)]
// The Family comes only from the verified token: no route or body field names one.
public sealed class ChildrenController(IChildService children) : ControllerBase
{
    internal const int MaxBodyBytes = 2048;

    [HttpPost]
    public async Task<ActionResult<ChildResponse>> Add([FromBody] AddChildBody body, CancellationToken cancellationToken)
    {
        if (Caller.From(User) is not { } caller)
        {
            return Unauthorized();
        }

        var result = await children.AddAsync(caller, body, cancellationToken);
        var response = ChildResponse.From(result.Child);

        return result.Created
            ? Created(new Uri($"/v1/family/children/{response.ChildId}", UriKind.Relative), response)
            : Ok(response);
    }

    [HttpPut("{childId:guid}")]
    public async Task<ActionResult<ChildResponse>> Edit(
        Guid childId,
        [FromBody] ChildDetailsBody body,
        CancellationToken cancellationToken
    )
    {
        if (Caller.From(User) is not { } caller)
        {
            return Unauthorized();
        }

        return Ok(ChildResponse.From(await children.EditAsync(caller, childId, body, cancellationToken)));
    }

    [HttpDelete("{childId:guid}")]
    public async Task<IActionResult> Delete(Guid childId, CancellationToken cancellationToken)
    {
        if (Caller.From(User) is not { } caller)
        {
            return Unauthorized();
        }

        await children.DeleteAsync(caller, childId, cancellationToken);

        return Accepted();
    }
}
