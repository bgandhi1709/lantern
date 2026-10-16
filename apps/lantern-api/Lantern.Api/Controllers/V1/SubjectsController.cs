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
[Route("v1/children/{childId:guid}/subjects")]
[EnableRateLimiting(RateLimits.Children)]
public sealed class SubjectsController(ISubjectService subjects, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageModel<SubjectModel>>> Get(
        Guid childId,
        [FromQuery] int? limit,
        [FromQuery] string cursor,
        CancellationToken cancellationToken
    ) => Ok(mapper.Map<PageModel<SubjectModel>>(await subjects.SubjectsAsync(childId, limit, cursor, cancellationToken)));
}
