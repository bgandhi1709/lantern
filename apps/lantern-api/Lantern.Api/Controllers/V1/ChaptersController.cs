using Asp.Versioning;
using Lantern.Api.Configuration;
using Lantern.Api.Models;
using Lantern.Base.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Lantern.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v1/children/{childId:guid}/subjects/{subjectId:maxlength(64)}/chapters")]
[EnableRateLimiting(RateLimits.Children)]
public sealed class ChaptersController(ISubjectService subjects, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChaptersPageModel>> Get(
        Guid childId,
        string subjectId,
        [FromQuery(Name = "q")] string query,
        [FromQuery] int? limit,
        [FromQuery] string cursor,
        CancellationToken cancellationToken
    ) => Ok(mapper.Map<ChaptersPageModel>(await subjects.ChaptersAsync(childId, subjectId, query, limit, cursor, cancellationToken)));
}
