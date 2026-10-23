using System.Diagnostics;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests
{
    public sealed class ResultProblemDetailsExtensionsTests
    {
        [Fact]
        public void ToProblemResult_ReturnsObjectResult_WhoseStatusCodeMatchesTheMapping()
        {
            Activity? previousActivity = null;
            try
            {
                previousActivity = Activity.Current;
                Activity.Current = null;
                var httpContext = new DefaultHttpContext();
                httpContext.TraceIdentifier = "ctx-trace-1";
                var result = Result.Failure(Error.NotFound("Example.NotFound", "The example does not exist."));

                var actionResult = result.ToProblemResult(httpContext);

                Assert.IsType<ObjectResult>(actionResult);
                Assert.Equal(StatusCodes.Status404NotFound, actionResult.StatusCode);
                var problemDetails = Assert.IsType<ProblemDetails>(actionResult.Value);
                Assert.Equal("Example.NotFound", problemDetails.Extensions["code"]);
                Assert.Equal("ctx-trace-1", problemDetails.Extensions["traceId"]);
            }
            finally
            {
                Activity.Current = previousActivity;
            }
        }

        [Fact]
        public void ToProblemResult_UsesTheCurrentActivityId_AsTraceIdentifier_WhenOneIsPresent()
        {
            Activity? previousActivity = null;
            try
            {
                previousActivity = Activity.Current;
                var activity = new Activity("ResultProblemDetailsExtensionsTests").Start();
                var httpContext = new DefaultHttpContext();
                httpContext.TraceIdentifier = "ctx-trace-1";
                var result = Result.Failure(Error.Conflict("Example.ExistsAlready", "The example already exists."));

                var actionResult = result.ToProblemResult(httpContext);

                var problemDetails = Assert.IsType<ProblemDetails>(actionResult.Value);
                Assert.Equal(activity.Id, problemDetails.Extensions["traceId"]);
                Assert.Equal(StatusCodes.Status409Conflict, actionResult.StatusCode);
            }
            finally
            {
                Activity.Current = previousActivity;
            }
        }

        [Fact]
        public void ToProblemResult_IncludesTheAccessorCorrelationId_InTheProblemDetails()
        {
            Activity? previousActivity = null;
            try
            {
                previousActivity = Activity.Current;
                Activity.Current = null;
                var httpContext = new DefaultHttpContext();
                httpContext.TraceIdentifier = "ctx-trace-1";
                var result = Result.Failure(Error.NotFound("Example.NotFound", "The example does not exist."));

                var actionResult = result.ToProblemResult(httpContext, new FixedCorrelationIdAccessor("corr-from-accessor"));

                var problemDetails = Assert.IsType<ProblemDetails>(actionResult.Value);
                Assert.Equal("corr-from-accessor", problemDetails.Extensions["correlationId"]);
                Assert.Equal("ctx-trace-1", problemDetails.Extensions["traceId"]);
            }
            finally
            {
                Activity.Current = previousActivity;
            }
        }

        [Fact]
        public void ToProblemResult_OmitsTheCorrelationId_WhenTheAccessorHasNone()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = "ctx-trace-1";
            var result = Result.Failure(Error.NotFound("Example.NotFound", "The example does not exist."));

            var actionResult = result.ToProblemResult(httpContext, new FixedCorrelationIdAccessor(null));

            var problemDetails = Assert.IsType<ProblemDetails>(actionResult.Value);
            Assert.False(problemDetails.Extensions.ContainsKey("correlationId"));
            Assert.Equal("ctx-trace-1", problemDetails.Extensions["traceId"]);
        }

        [Fact]
        public void ToProblemResult_Throws_WhenTheResultIsSuccessful()
        {
            var httpContext = new DefaultHttpContext();

            Assert.Throws<InvalidOperationException>(() => Result.Success().ToProblemResult(httpContext));
        }

        [Fact]
        public void ToProblemResult_Throws_WhenHttpContextIsMissing()
        {
            var result = Result.Failure(Error.NotFound("Example.NotFound", "The example does not exist."));

            Assert.Throws<ArgumentNullException>(() => ResultProblemDetailsExtensions.ToProblemResult(result, httpContext: null!));
        }
    }
}
