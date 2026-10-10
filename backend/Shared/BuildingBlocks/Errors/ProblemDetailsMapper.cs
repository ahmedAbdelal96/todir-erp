using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Primitives.Errors;

namespace Shared.BuildingBlocks.Errors;

/// <summary>Maps stable domain error codes to transport-safe RFC 7807 responses.</summary>
public static class ProblemDetailsMapper
{
    public static ProblemDetails ToProblemDetails(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var problemDetails = new ProblemDetails
        {
            Status = GetStatusCode(error),
            Title = GetTitle(error),
            Detail = error.Message
        };
        problemDetails.Extensions["code"] = error.Code;

        if (error is ValidationError validationError && validationError.FieldErrors.Count > 0)
        {
            problemDetails.Extensions["errors"] = validationError.FieldErrors;
        }

        return problemDetails;
    }

    private static int GetStatusCode(DomainError error) => error switch
    {
        ValidationError => StatusCodes.Status400BadRequest,
        ForbiddenError => StatusCodes.Status403Forbidden,
        NotFoundError => StatusCodes.Status404NotFound,
        ConflictError => StatusCodes.Status409Conflict,
        ConcurrencyError => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    private static string GetTitle(DomainError error) => error switch
    {
        ValidationError => "Validation failed",
        ForbiddenError => "Forbidden",
        NotFoundError => "Not found",
        ConflictError => "Conflict",
        ConcurrencyError => "Concurrency conflict",
        _ => "Request failed"
    };
}
