using System.Net;
using Lantern.Api.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Exceptions;

// Only the exceptions a caller can legitimately trigger get mapped to a response. Anything else
// (storage, unexpected bugs) is a hard stop: TryHandleAsync returns false and it becomes a real,
// unhandled 500 instead of a glossed-over "something went wrong, try again."
internal sealed class RegistrationExceptionHandler(ILogger<RegistrationExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        switch (exception)
        {
            case AlreadyRegisteredException:
                Log.AlreadyRegistered(logger);
                await WriteProblemAsync(
                    httpContext,
                    HttpStatusCode.Conflict,
                    "already-registered",
                    "Already registered",
                    "This account is already registered.",
                    cancellationToken
                );
                return true;

            case NotRegisteredException:
                await WriteProblemAsync(
                    httpContext,
                    HttpStatusCode.NotFound,
                    "not-registered",
                    "Not registered",
                    "This account has not registered yet.",
                    cancellationToken
                );
                return true;

            // Static, developer-authored validation text meant for the caller. Anything else
            // (storage, tokens) can hold account names or request ids and must never reach the
            // response, which is exactly why those fall through to the default handler below.
            case InvalidRegistrationException invalid:
                Log.RegistrationInvalid(logger, invalid.Message);
                await WriteProblemAsync(
                    httpContext,
                    HttpStatusCode.BadRequest,
                    "invalid-registration",
                    "Invalid registration",
                    invalid.Message,
                    cancellationToken
                );
                return true;

            case ChildNotFoundException:
                await WriteProblemAsync(
                    httpContext,
                    HttpStatusCode.NotFound,
                    "child-not-found",
                    "Child not found",
                    "No such child in this family.",
                    cancellationToken
                );
                return true;

            case ChildLimitReachedException:
                await WriteProblemAsync(
                    httpContext,
                    HttpStatusCode.Conflict,
                    "child-limit-reached",
                    "Child limit reached",
                    "A family holds at most six children.",
                    cancellationToken
                );
                return true;

            case ChildDeletingException:
                await WriteProblemAsync(
                    httpContext,
                    HttpStatusCode.Conflict,
                    "child-deleting",
                    "Child is being deleted",
                    "This child is being deleted.",
                    cancellationToken
                );
                return true;

            case FamilyChangedException:
                await WriteProblemAsync(
                    httpContext,
                    HttpStatusCode.Conflict,
                    "family-changed",
                    "Family changed",
                    "The family changed at the same moment; try again.",
                    cancellationToken
                );
                return true;

            default:
                return false;
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode status,
        string code,
        string title,
        string detail,
        CancellationToken cancellationToken
    )
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;

        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
    }
}
