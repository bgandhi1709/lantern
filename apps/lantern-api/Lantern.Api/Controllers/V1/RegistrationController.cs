using Asp.Versioning;
using Lantern.Api.Auth;
using Lantern.Api.Contracts;
using Lantern.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("v1")]
// The caller comes only from the verified token: no route or body field names a family.
public sealed class RegistrationController(IRegistrationService registration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<FamilyResponse>> Register(
        [FromBody] RegisterBody body,
        CancellationToken cancellationToken
    )
    {
        if (Caller.From(this.User) is not { } caller)
        {
            return this.Unauthorized();
        }

        var family = await registration.RegisterAsync(caller, body, cancellationToken);

        return this.CreatedAtAction(nameof(Me), null, FamilyResponse.From(family));
    }

    [HttpGet("me")]
    public async Task<ActionResult<FamilyResponse>> Me(CancellationToken cancellationToken)
    {
        if (Caller.From(this.User) is not { } caller)
        {
            return this.Unauthorized();
        }

        var family = await registration.GetAsync(caller, cancellationToken);

        return this.Ok(FamilyResponse.From(family));
    }
}
