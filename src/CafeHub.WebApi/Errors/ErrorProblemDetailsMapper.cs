using CafeHub.Application.Abstractions.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.Errors
{
    /// <summary>
    /// Maps every <see cref="Error"/> to exactly one RFC 9457 ProblemDetails shape. Mapping table, the single
    /// source of truth for error responses: ErrorType.Validation to 400, ErrorType.Unauthorized to 401,
    /// ErrorType.Forbidden to 403, ErrorType.NotFound to 404, ErrorType.Conflict to 409, ErrorType.Failure to 500.
    /// Each body carries the machine-readable "code" extension (the Error.Code) and a "traceId" extension;
    /// validation failures become a ValidationProblemDetails whose Errors dictionary lists the field-level messages.
    /// Titles and "type" links mirror the RFC 9110 defaults of ASP.NET Core (ProblemDetailsDefaults).
    /// </summary>
    public static class ErrorProblemDetailsMapper
    {
        private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
        private const string UnauthorizedType = "https://tools.ietf.org/html/rfc9110#section-15.5.2";
        private const string ForbiddenType = "https://tools.ietf.org/html/rfc9110#section-15.5.4";
        private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
        private const string ConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
        private const string InternalServerErrorType = "https://tools.ietf.org/html/rfc9110#section-15.6.1";

        private const string BadRequestTitle = "Bad Request";
        private const string UnauthorizedTitle = "Unauthorized";
        private const string ForbiddenTitle = "Forbidden";
        private const string NotFoundTitle = "Not Found";
        private const string ConflictTitle = "Conflict";
        private const string InternalServerErrorTitle = "Internal Server Error";

        public static ProblemDetails Map(Error error, string traceIdentifier)
        {
            if (error is null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (traceIdentifier is null)
            {
                throw new ArgumentNullException(nameof(traceIdentifier));
            }

            if (error is ValidationError validationError)
            {
                return MapValidation(validationError, traceIdentifier);
            }

            return error.Type switch
            {
                ErrorType.Unauthorized => MapStandard(error, traceIdentifier, StatusCodes.Status401Unauthorized),
                ErrorType.Forbidden => MapStandard(error, traceIdentifier, StatusCodes.Status403Forbidden),
                ErrorType.NotFound => MapStandard(error, traceIdentifier, StatusCodes.Status404NotFound),
                ErrorType.Conflict => MapStandard(error, traceIdentifier, StatusCodes.Status409Conflict),
                _ => MapStandard(error, traceIdentifier, StatusCodes.Status500InternalServerError),
            };
        }

        private static ProblemDetails MapValidation(ValidationError error, string traceIdentifier)
        {
            var fieldMessages = new Dictionary<string, List<string>>();
            foreach (var fieldError in error.Errors)
            {
                if (!fieldMessages.TryGetValue(fieldError.Field, out var messages))
                {
                    messages = new List<string>();
                    fieldMessages.Add(fieldError.Field, messages);
                }

                messages.Add(fieldError.Message);
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var field in fieldMessages)
            {
                errors.Add(field.Key, field.Value.ToArray());
            }

            var problemDetails = new ValidationProblemDetails(errors)
            {
                Type = BadRequestType,
                Title = BadRequestTitle,
                Status = StatusCodes.Status400BadRequest,
                Detail = error.Message,
            };
            problemDetails.Extensions["code"] = error.Code;
            problemDetails.Extensions["traceId"] = traceIdentifier;

            return problemDetails;
        }

        private static ProblemDetails MapStandard(Error error, string traceIdentifier, int statusCode)
        {
            var (type, title) = StatusCodeDescription(statusCode);

            var problemDetails = new ProblemDetails
            {
                Type = type,
                Title = title,
                Status = statusCode,
                Detail = error.Message,
            };
            problemDetails.Extensions["code"] = error.Code;
            problemDetails.Extensions["traceId"] = traceIdentifier;

            return problemDetails;
        }

        private static (string Type, string Title) StatusCodeDescription(int statusCode)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => (BadRequestType, BadRequestTitle),
                StatusCodes.Status401Unauthorized => (UnauthorizedType, UnauthorizedTitle),
                StatusCodes.Status403Forbidden => (ForbiddenType, ForbiddenTitle),
                StatusCodes.Status404NotFound => (NotFoundType, NotFoundTitle),
                StatusCodes.Status409Conflict => (ConflictType, ConflictTitle),
                StatusCodes.Status500InternalServerError => (InternalServerErrorType, InternalServerErrorTitle),
                _ => throw new InvalidOperationException($"No documented ProblemDetails mapping exists for status {statusCode}."),
            };
        }
    }
}
