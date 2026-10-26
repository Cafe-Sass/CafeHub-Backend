using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Onboarding.Controllers
{
    public sealed class OnboardingRequestsControllerStatusTests
    {
        private const string TraceIdentifier = "trace-1";

        private static OnboardingRequestsController CreateController(RecordingSender sender)
        {
            return new OnboardingRequestsController(sender)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        [Fact]
        public async Task MalformedId_ReturnsTheSameNotFoundAsUnknownId()
        {
            var unknownIdSender = new RecordingSender
            {
                Response = Result.Failure<CafeHub.Application.Onboarding.DTOs.OnboardingStatusResponse>(
                    CafeHub.Application.Onboarding.OnboardingErrors.RequestNotFound()),
            };
            var unknownController = CreateController(unknownIdSender);
            var malformedSender = new RecordingSender();
            var malformedController = CreateController(malformedSender);

            var malformedResult = await malformedController.GetStatus("not-a-guid", CancellationToken.None);
            var unknownResult = await unknownController.GetStatus(Guid.CreateVersion7().ToString(), CancellationToken.None);

            var malformedProblem = Assert.IsType<ObjectResult>(malformedResult.Result);
            var unknownProblem = Assert.IsType<ObjectResult>(unknownResult.Result);
            Assert.Equal(unknownProblem.StatusCode, malformedProblem.StatusCode);
            Assert.Equal(StatusCodes.Status404NotFound, malformedProblem.StatusCode);

            var malformedBody = Assert.IsType<ProblemDetails>(malformedProblem.Value!);
            var unknownBody = Assert.IsType<ProblemDetails>(unknownProblem.Value!);
            Assert.Equal(unknownBody.Title, malformedBody.Title);
            Assert.Equal(unknownBody.Type, malformedBody.Type);
            Assert.Equal(
                unknownBody.Extensions["code"],
                malformedBody.Extensions["code"]);
            Assert.Equal("Onboarding.RequestNotFound", malformedBody.Extensions["code"]);
            Assert.Equal(0, malformedSender.CallCount);
            Assert.Equal(1, unknownIdSender.CallCount);
        }

        [Fact]
        public async Task KnownId_SendsQueryWithParsedRequestId()
        {
            var requestId = Guid.CreateVersion7();
            var sender = new RecordingSender
            {
                Response = Result.Success(
                    new CafeHub.Application.Onboarding.DTOs.OnboardingStatusResponse("registered")),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.GetStatus(requestId.ToString(), CancellationToken.None);

            var response = Assert.IsType<CafeHub.Application.Onboarding.DTOs.OnboardingStatusResponse>(
                Assert.IsType<OkObjectResult>(actionResult.Result).Value!);
            Assert.Equal("registered", response.Status);
            var query = Assert.IsType<GetOnboardingStatusQuery>(sender.LastRequest);
            Assert.Equal(requestId, query.RequestId);
        }
    }
}
