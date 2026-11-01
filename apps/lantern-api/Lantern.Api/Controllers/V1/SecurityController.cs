using Asp.Versioning;
using Lantern.Api.Models;
using Lantern.Base.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v1/security/code")]
[RequestSizeLimit(MaxBodyBytes)]
public sealed class SecurityController(IOneTimeCodeService codes) : ControllerBase
{
    private const int MaxBodyBytes = 1024;

    [HttpPost]
    public async Task<ActionResult<CodeChallengeModel>> Issue([FromBody] CodeRequest request, CancellationToken cancellationToken) =>
        Ok(new CodeChallengeModel { Challenge = await codes.RequestAsync(request.Operation, request.Target, cancellationToken) });

    [HttpPost("verify")]
    public ActionResult<CodeProofModel> Verify([FromBody] CodeVerifyRequest request) =>
        Ok(new CodeProofModel { Proof = codes.Verify(request.Challenge, request.Code) });
}
