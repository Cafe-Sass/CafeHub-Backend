using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Identity.Queries;
using CafeHub.Domain.Identity;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests
{
    public sealed class AdminControllerTests
    {
        private const string AdminEmail = "current.admin@cafehub.app";
        private const string TraceIdentifier = "trace-admin-me";

        private static AdminController CreateController(ISender sender, ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            return new AdminController(sender, correlationIdAccessor)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        [Fact]
        public async Task Me_SendsTheCurrentAdminQuery_AndReturnsTheAdministratorOnSuccess()
        {
            var response = new CurrentAdminResponse
            {
                Id = Guid.NewGuid(),
                Email = AdminEmail,
                Roles = new[] { PlatformAdminRoles.SuperAdmin },
            };
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var actionResult = await controller.Me(cancellationToken);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            Assert.Equal(1, sender.CallCount);
            Assert.IsType<GetCurrentAdminQuery>(sender.LastRequest);
            Assert.Equal(cancellationToken, sender.LastCancellationToken);
        }

        [Fact]
        public async Task Me_WhenTheQueryFails_MapsTheSafeUnauthorizedProblemDetails()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<CurrentAdminResponse>(AuthErrors.CurrentAdminUnavailable()),
            };
            var controller = CreateController(sender, new FixedCorrelationIdAccessor("corr-admin-me"));

            var actionResult = await controller.Me(CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
            Assert.Equal(AuthErrors.CurrentAdminUnavailableCode, problemDetails.Extensions["code"]);
            Assert.Equal(AuthErrors.CurrentAdminUnavailableMessage, problemDetails.Detail);
            Assert.Equal(TraceIdentifier, problemDetails.Extensions["traceId"]);
            Assert.Equal("corr-admin-me", problemDetails.Extensions["correlationId"]);
            Assert.DoesNotContain(AdminEmail, problemDetails.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new AdminController(null!));
        }
    }

    internal sealed class RecordingSender : ISender
    {
        public object? Response { get; set; }

        public int CallCount { get; private set; }

        public IBaseRequest? LastRequest { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            LastCancellationToken = cancellationToken;

            return Task.FromResult((TResponse)Response!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw new NotSupportedException("The controller must send a request with a response.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The controller must send a typed request.");
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The controller does not create streams.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The controller does not create streams.");
        }
    }
}
