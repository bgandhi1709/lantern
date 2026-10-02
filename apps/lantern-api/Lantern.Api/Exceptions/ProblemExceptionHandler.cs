using System.Globalization;
using System.Net;
using Lantern.Api.Logging;
using Lantern.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Exceptions;

// Only the exceptions a caller can legitimately trigger get mapped to a response. Anything else (storage, unexpected
// bugs) is a hard stop: TryHandleAsync returns false and it becomes a real, unhandled 500.
internal sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        (HttpStatusCode Status, string Code, string Title, string Detail)? problem = exception switch
        {
            AlreadyRegisteredException => (HttpStatusCode.Conflict, "already-registered", "Already registered", "This account is already registered."),
            NotRegisteredException => (HttpStatusCode.NotFound, "not-registered", "Not registered", "This account has not registered yet."),
            // Static, developer-authored validation text meant for the caller; nothing else may reach the response.
            InvalidRequestException invalid => (HttpStatusCode.BadRequest, "invalid-registration", "Invalid registration", invalid.Message),
            CallerNotIdentifiedException => (HttpStatusCode.Unauthorized, "caller-not-identified", "Not signed in", "The token does not identify a caller."),
            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                $"{notFound.Resource}-not-found",
                $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(notFound.Resource)} not found",
                notFound.Message
            ),
            ChildLimitReachedException => (HttpStatusCode.Conflict, "child-limit-reached", "Child limit reached", "A family holds at most six children."),
            ChildDeletingException => (HttpStatusCode.Conflict, "child-deleting", "Child is being deleted", "This child is being deleted."),
            FamilyChangedException => (HttpStatusCode.Conflict, "family-changed", "Family changed", "The family changed at the same moment; try again."),
            _ => null,
        };

        if (problem is not { } found)
        {
            return false;
        }

        switch (exception)
        {
            case AlreadyRegisteredException:
                Log.AlreadyRegistered(logger);
                break;
            case InvalidRequestException:
                Log.RegistrationInvalid(logger, exception.Message);
                break;
        }

        httpContext.Response.StatusCode = (int)found.Status;
        httpContext.Response.ContentType = "application/problem+json";

        var details = new ProblemDetails
        {
            Status = (int)found.Status,
            Title = found.Title,
            Detail = found.Detail,
            Instance = httpContext.Request.Path,
        };
        details.Extensions["code"] = found.Code;

        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);

        return true;
    }
}
