using Asp.Versioning;
using Lantern.Api.Auth;
using Lantern.Api.Configuration;
using Lantern.Api.Contracts;
using Lantern.Api.Services.Interfaces;
using MapsterMapper;
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
public sealed class ChildrenController(IChildService children, ICurrentCaller currentCaller, IMapper mapper)
    : ControllerBase
{
    const int MaxBodyBytes = 2048;

    [HttpPost]
    public async Task<ActionResult<ChildResponse>> Add([FromBody] AddChildBody body, CancellationToken cancellationToken)
    {
        var result = await children.AddAsync(currentCaller.Require(), body, cancellationToken);
        var response = mapper.Map<ChildResponse>(result.Child);

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
        return Ok(mapper.Map<ChildResponse>(await children.EditAsync(currentCaller.Require(), childId, body, cancellationToken)));
    }

    [HttpDelete("{childId:guid}")]
    public async Task<IActionResult> Delete(Guid childId, CancellationToken cancellationToken)
    {
        await children.DeleteAsync(currentCaller.Require(), childId, cancellationToken);

        return Accepted();
    }
}
