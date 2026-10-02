using Asp.Versioning;
using Lantern.Api.Models;
using Lantern.Base.Services;
using Lantern.Core.Models;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("v1")]
public sealed class RegistrationController(IFamilyService families, IMapper mapper)
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
}
