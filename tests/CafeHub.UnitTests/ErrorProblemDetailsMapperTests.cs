using CafeHub.Application.Abstractions.Results;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests
{
    public sealed class ErrorProblemDetailsMapperTests
    {
        public static TheoryData<ErrorType, int, string, string> MappingTable => new()
        {
            { ErrorType.Unauthorized, StatusCodes.Status401Unauthorized, "Unauthorized", "https://tools.ietf.org/html/rfc9110#section-15.5.2" },
            { ErrorType.Forbidden, StatusCodes.Status403Forbidden, "Forbidden", "https://tools.ietf.org/html/rfc9110#section-15.5.4" },
            { ErrorType.NotFound, StatusCodes.Status404NotFound, "Not Found", "https://tools.ietf.org/html/rfc9110#section-15.5.5" },
            { ErrorType.Conflict, StatusCodes.Status409Conflict, "Conflict", "https://tools.ietf.org/html/rfc9110#section-15.5.10" },
            { ErrorType.Failure, StatusCodes.Status500InternalServerError, "Internal Server Error", "https://tools.ietf.org/html/rfc9110#section-15.6.1" },
        };

        [Theory]
        [MemberData(nameof(MappingTable))]
        public void Map_EachErrorType_ProducesTheDocumentedStatus_Title_Type_Code_Detail_AndTraceId(ErrorType errorType, int expectedStatus, string expectedTitle, string expectedType)
        {
            var error = CreateError(errorType);

            var problemDetails = ErrorProblemDetailsMapper.Map(error, "trace-each-1");

            Assert.Equal(expectedStatus, problemDetails.Status);
            Assert.Equal(expectedType, problemDetails.Type);
            Assert.Equal(expectedTitle, problemDetails.Title);
            Assert.Equal(error.Message, problemDetails.Detail);
            Assert.Equal(error.Code, problemDetails.Extensions["code"]);
            Assert.Equal("trace-each-1", problemDetails.Extensions["traceId"]);
        }

        [Fact]
        public void Map_ValidationError_Produces400_WithEveryFieldErrorListed()
        {
            var error = ValidationError.Create(new[]
            {
                new FieldError("Address", "The address is required."),
                new FieldError("Address", "The address is too long."),
                new FieldError("Phone", "The phone number is not valid."),
            });

            var problemDetails = ErrorProblemDetailsMapper.Map(error, "trace-validation-1");

            var validation = Assert.IsType<ValidationProblemDetails>(problemDetails);
            Assert.Equal(StatusCodes.Status400BadRequest, validation.Status);
            Assert.Equal("Bad Request", validation.Title);
            Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", validation.Type);
            Assert.Equal("One or more validation errors occurred.", validation.Detail);
            Assert.Equal("Validation.Failed", validation.Extensions["code"]);
            Assert.Equal("trace-validation-1", validation.Extensions["traceId"]);
            Assert.Equal(new[] { "Address", "Phone" }, validation.Errors.Keys);
            Assert.Equal(new[] { "The address is required.", "The address is too long." }, validation.Errors["Address"]);
            Assert.Equal(new[] { "The phone number is not valid." }, validation.Errors["Phone"]);
        }

        [Fact]
        public void Map_TwoDifferentErrorsWithSameType_MapToTheSameStatus_WithDifferentCodes()
        {
            var first = Error.NotFound("Example.NotFound", "The example does not exist.");
            var second = Error.NotFound("Example.BranchMissing", "The branch does not exist.");

            var firstDetails = ErrorProblemDetailsMapper.Map(first, "trace-first");
            var secondDetails = ErrorProblemDetailsMapper.Map(second, "trace-second");

            Assert.Equal(firstDetails.Status, secondDetails.Status);
            Assert.Equal(StatusCodes.Status404NotFound, firstDetails.Status);
            Assert.Equal("Example.NotFound", firstDetails.Extensions["code"]);
            Assert.Equal("Example.BranchMissing", secondDetails.Extensions["code"]);
        }

        [Fact]
        public void Map_Throws_WhenErrorOrTraceIdentifierIsMissing()
        {
            Assert.Throws<ArgumentNullException>(() => ErrorProblemDetailsMapper.Map(null!, "trace-a"));
            Assert.Throws<ArgumentNullException>(() => ErrorProblemDetailsMapper.Map(Error.Failure("Example.Unexpected", "A human readable message."), null!));
        }

        private static Error CreateError(ErrorType errorType)
        {
            return errorType switch
            {
                ErrorType.Unauthorized => Error.Unauthorized("Example.NoSession", "A human readable message."),
                ErrorType.Forbidden => Error.Forbidden("Example.NoAccess", "A human readable message."),
                ErrorType.NotFound => Error.NotFound("Example.NotFound", "A human readable message."),
                ErrorType.Conflict => Error.Conflict("Example.ExistsAlready", "A human readable message."),
                _ => Error.Failure("Example.Unexpected", "A human readable message."),
            };
        }
    }
}
