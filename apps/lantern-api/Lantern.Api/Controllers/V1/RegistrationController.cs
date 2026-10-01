using Asp.Versioning;
using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Services.Interfaces;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("v1")]
[Authorize]
// The caller comes only from the verified token: no route or body field names a family.
public sealed class RegistrationController(IRegistrationService registration, ICurrentCaller currentCaller, IMapper mapper)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<FamilyResponse>> Register(
        [FromBody] RegisterBody body,
        CancellationToken cancellationToken
    )
    {
        var family = await registration.RegisterAsync(currentCaller.Require(), body, cancellationToken);

        return CreatedAtAction(nameof(Me), null, mapper.Map<FamilyResponse>(family));
    }

    [HttpGet("me")]
    public async Task<ActionResult<FamilyResponse>> Me(CancellationToken cancellationToken)
    {
        var family = await registration.GetAsync(currentCaller.Require(), cancellationToken);

        return Ok(mapper.Map<FamilyResponse>(family));
    }
}
