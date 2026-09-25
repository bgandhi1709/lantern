using System.Net;
using Azure;
using Lantern.Api.Exceptions;
using Lantern.Api.Logging;
using Microsoft.AspNetCore.Mvc;

namespace Lantern.Api.Middleware;

internal sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    // Responses carry fixed text only: exception messages can hold account names and request ids.
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AlreadyRegisteredException)
        {
            Log.AlreadyRegistered(logger);
            await WriteProblemAsync(
                context,
                HttpStatusCode.Conflict,
                "already-registered",
                "Already registered",
                "This account is already registered."
            );
        }
        catch (NotRegisteredException)
        {
            await WriteProblemAsync(
                context,
                HttpStatusCode.NotFound,
                "not-registered",
                "Not registered",
                "This account has not registered yet."
            );
        }
        catch (InvalidRegistrationException ex)
        {
            Log.RegistrationInvalid(logger, ex.Message);
            await WriteProblemAsync(
                context,
                HttpStatusCode.BadRequest,
                "invalid-registration",
                "Invalid registration",
                ex.Message
            );
        }
        catch (RequestFailedException ex)
        {
            Log.StorageFailed(logger, ex, ex.Status);
            await WriteProblemAsync(
                context,
                HttpStatusCode.ServiceUnavailable,
                "storage-unavailable",
                "Storage unavailable",
                "The service cannot reach its storage right now. Try again shortly."
            );
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode status,
        string code,
        string title,
        string detail
    )
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
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

        await context.Response.WriteAsJsonAsync(problem);
    }
}
