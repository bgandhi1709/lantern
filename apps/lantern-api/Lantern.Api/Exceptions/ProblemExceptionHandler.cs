using System.Net;
using Lantern.Api.Logging;
using Lantern.Core.Constants;
using Lantern.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Exceptions;

// Only the failures a caller can legitimately trigger get mapped to a response. Anything else (storage, unexpected
// bugs) is a hard stop: TryHandleAsync returns false and it becomes a real, unhandled 500.
internal sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is not LanternException failure)
        {
            return false;
        }

        var (status, code, title, detail) = Describe(failure);

        switch (failure.Code)
        {
            case LanternErrorCode.AlreadyRegistered:
                Log.AlreadyRegistered(logger);
                break;
            case LanternErrorCode.InvalidRequest:
                Log.RegistrationInvalid(logger, failure.Message);
                break;
        }

        httpContext.Response.StatusCode = (int)status;
        httpContext.Response.ContentType = "application/problem+json";

        var details = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        details.Extensions["code"] = code;

        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);

        return true;
    }

    // The wire codes are part of the API contract: never rename one once released.
    private static (HttpStatusCode Status, string Code, string Title, string Detail) Describe(LanternException failure) =>
        failure.Code switch
        {
            LanternErrorCode.AlreadyRegistered => (HttpStatusCode.Conflict, "already-registered", "Already registered", "This account is already registered."),
            LanternErrorCode.NotRegistered => (HttpStatusCode.NotFound, "not-registered", "Not registered", "This account has not registered yet."),
            // Static, developer-authored validation text meant for the caller; nothing else may reach the response.
            LanternErrorCode.InvalidRequest => (HttpStatusCode.BadRequest, "invalid-registration", "Invalid registration", failure.Message),
            LanternErrorCode.CallerNotIdentified => (HttpStatusCode.Unauthorized, "caller-not-identified", "Not signed in", "The token does not identify a caller."),
            LanternErrorCode.ChildNotFound => (HttpStatusCode.NotFound, "child-not-found", "Child not found", "No such child in this family."),
            LanternErrorCode.FamilyNotFound => (HttpStatusCode.NotFound, "family-not-found", "Family not found", "No such family."),
            LanternErrorCode.ChildLimitReached => (HttpStatusCode.Conflict, "child-limit-reached", "Child limit reached", "A family holds at most six children."),
            LanternErrorCode.ChildDeleting => (HttpStatusCode.Conflict, "child-deleting", "Child is being deleted", "This child is being deleted."),
            LanternErrorCode.FamilyIdTaken => (HttpStatusCode.Conflict, "family-id-taken", "Family id taken", "A family with this id already exists."),
            LanternErrorCode.ClassNotAvailable => (HttpStatusCode.BadRequest, "class-not-available", "Class not available", "Lantern does not have books for this Class on the Family's Board yet."),
            LanternErrorCode.FamilyChanged => (HttpStatusCode.Conflict, "family-changed", "Family changed", "The family changed at the same moment; try again."),
            _ => throw new InvalidOperationException($"No problem mapping for {failure.Code}."),
        };
}
