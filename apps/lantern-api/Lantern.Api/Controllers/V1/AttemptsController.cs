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
[Route("v1/children/{childId:guid}/chapters/{chapterId:maxlength(64)}/attempts")]
[EnableRateLimiting(RateLimits.Children)]
public sealed class AttemptsController(ISubjectService subjects, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChapterHistoryModel>> Get(
        Guid childId,
        string chapterId,
        [FromQuery] int? limit,
        [FromQuery] string cursor,
        CancellationToken cancellationToken
    ) => Ok(mapper.Map<ChapterHistoryModel>(await subjects.AttemptsAsync(childId, chapterId, limit, cursor, cancellationToken)));
}
