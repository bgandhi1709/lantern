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
[Route("v1/family/children")]
[EnableRateLimiting(RateLimits.Children)]
[RequestSizeLimit(MaxBodyBytes)]
public sealed class ChildrenController(IChildService children, IMapper mapper)
    : ResourceControllerBase<ChildModel, Child>(children, mapper)
{
    private const int MaxBodyBytes = 2048;

    [HttpPost]
    public async Task<ActionResult<ChildModel>> Add([FromBody] ChildAddModel model, CancellationToken cancellationToken)
    {
        var (child, created) = await children.AddOrGetAsync(Mapper.Map<Child>(model), cancellationToken);
        var response = ToModel(child);

        return created ? Created(new Uri($"/v1/family/children/{response.ChildId}", UriKind.Relative), response) : Ok(response);
    }

    [HttpPut("{childId:guid}")]
    public async Task<ActionResult<ChildModel>> Edit(
        Guid childId,
        [FromBody] ChildUpdateModel model,
        CancellationToken cancellationToken
    )
    {
        var child = Mapper.Map<Child>(model);
        child.ChildId = childId;

        return Ok(ToModel(await Service.UpdateAsync(child, cancellationToken)));
    }

    // 202: the delete finishes in Lantern.Functions (ADR-0003).
    [HttpDelete("{childId:guid}")]
    public async Task<IActionResult> Delete(Guid childId, CancellationToken cancellationToken)
    {
        await Service.RemoveAsync(childId, cancellationToken);

        return Accepted();
    }
}
