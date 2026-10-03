using Asp.Versioning;
using Lantern.Api.Configuration;
using Lantern.Api.Models;
using Lantern.Base.Services;
using Lantern.Core.Models;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lantern.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("v1")]
public sealed class FamilyController(IFamilyService families, IMapper mapper)
    : ResourceControllerBase<FamilyModel, Family>(families, mapper)
{
    [HttpPost("register")]
    public async Task<ActionResult<FamilyModel>> Register(
        [FromBody] FamilyRegisterRequest request,
        CancellationToken cancellationToken
    ) =>
        CreatedAtAction(
            nameof(Me),
            null,
            ToModel(await families.RegisterAsync(Mapper.Map<Registration>(request), cancellationToken))
        );

    [HttpGet("me")]
    public async Task<ActionResult<FamilyModel>> Me(CancellationToken cancellationToken) =>
        Ok(ToModel(await families.MeAsync(cancellationToken)));

    // 204: the caller is unregistered at once; the rest of the erasure finishes in Lantern.Functions.
    [HttpDelete("family")]
    [EnableRateLimiting(RateLimits.Children)]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await families.EraseAsync(cancellationToken);

        return NoContent();
    }
}
