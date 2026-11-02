using System.Collections.Generic;
using System.Reflection;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile;
using CafeHub.Application.CafeProfile.Commands;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.CafeProfile.Queries;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.CafeProfile
{
    public sealed class TenantCafeProfileControllerTests
    {
        private const string TraceIdentifier = "trace-cafe-profile";

        private static TenantCafeProfileController CreateController(ISender sender)
        {
            return new TenantCafeProfileController(sender, new TestCorrelationIdAccessor("corr-cafe-profile"))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = TraceIdentifier },
                },
            };
        }

        [Fact]
        public async Task Get_SendsGetCafeProfileQuery_AndReturnsTheProfileOnSuccess()
        {
            var response = new CafeProfileResponse
            {
                CafeName = "My Cafe",
                LogoMediaReference = "media-ref-123",
                PrimaryColor = "#ff0000",
                SecondaryColor = "#00ff00",
                AccentColor = "#0000ff",
                ContactPhone = "+1-555-0100",
                ContactEmail = "contact@example.com",
                ContactAddress = "123 Main St",
                Timezone = "America/New_York",
                ConcurrencyToken = "12345",
            };
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.Get(CancellationToken.None);

            Assert.Same(response, Assert.IsType<OkObjectResult>(actionResult.Result).Value);
            Assert.IsType<GetCafeProfileQuery>(sender.LastRequest);
        }

        [Fact]
        public async Task Get_WhenTheQueryReturnsNotFound_MapsTo404()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<CafeProfileResponse>(CafeProfileErrors.NotFound()),
            };
            var controller = CreateController(sender);

            var actionResult = await controller.Get(CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal(CafeProfileErrors.NotFoundCode, problem.Extensions["code"]);
            Assert.Equal(TraceIdentifier, problem.Extensions["traceId"]);
            Assert.Equal("corr-cafe-profile", problem.Extensions["correlationId"]);
        }

        [Fact]
        public async Task Put_SendsUpdateCafeProfileCommand_AndReturnsTheProfileOnSuccess()
        {
            var response = new CafeProfileResponse
            {
                CafeName = "My Cafe",
                LogoMediaReference = null,
                PrimaryColor = "#ff0000",
                SecondaryColor = null,
                AccentColor = null,
                ContactPhone = null,
                ContactEmail = null,
                ContactAddress = null,
                Timezone = "America/New_York",
                ConcurrencyToken = "12345",
            };
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var request = new UpdateCafeProfileRequest(
                "My Cafe", null, "#ff0000", null, null, null, null, null, "America/New_York", "12345");

            var actionResult = await controller.Put(request, CancellationToken.None);

            Assert.Same(response, Assert.IsType<OkObjectResult>(actionResult.Result).Value);
            var command = Assert.IsType<UpdateCafeProfileCommand>(sender.LastRequest);
            Assert.Equal("My Cafe", command.CafeName);
            Assert.Equal("America/New_York", command.Timezone);
            Assert.Equal("12345", command.ConcurrencyToken);
        }

        [Fact]
        public async Task Put_WhenConcurrencyConflict_MapsTo409()
        {
            var sender = new RecordingSender
            {
                Response = Result.Failure<CafeProfileResponse>(CafeProfileErrors.ConcurrencyConflict()),
            };
            var controller = CreateController(sender);

            var request = new UpdateCafeProfileRequest(
                "My Cafe", null, "#ff0000", null, null, null, null, null, "America/New_York", "99999");

            var actionResult = await controller.Put(request, CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
            Assert.Equal(CafeProfileErrors.ConcurrencyConflictCode, problem.Extensions["code"]);
        }

        [Fact]
        public void GetAction_UsesTenantRouteAndTenantUserAuthorization()
        {
            var controllerType = typeof(TenantCafeProfileController);
            var route = controllerType.GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal("t/{slug}/api/v1/cafe-profile", route!.Template);

            var action = controllerType.GetMethod(nameof(TenantCafeProfileController.Get))!;
            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
            Assert.NotNull(authorize);
            Assert.Equal(TenantPolicies.TenantUser, authorize!.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(controllerType.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void PutAction_UsesTenantOwnerAuthorization()
        {
            var controllerType = typeof(TenantCafeProfileController);
            var action = controllerType.GetMethod(nameof(TenantCafeProfileController.Put))!;
            Assert.NotNull(action.GetCustomAttribute<HttpPutAttribute>());
            var authorize = action.GetCustomAttribute<TenantAuthorizeAttribute>();
            Assert.NotNull(authorize);
            Assert.Equal(TenantPolicies.TenantOwner, authorize!.Policy);
            Assert.Equal(TenantAuthenticationSchemes.Bearer, authorize.AuthenticationSchemes);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void TheController_RequiresTheSender()
        {
            Assert.Throws<ArgumentNullException>(() => new TenantCafeProfileController(null!));
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; set; }
            public object? LastRequest { get; private set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
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

        private sealed class TestCorrelationIdAccessor : ICorrelationIdAccessor
        {
            public TestCorrelationIdAccessor(string? correlationId)
            {
                CorrelationId = correlationId;
            }

            public string? CorrelationId { get; }
        }
    }
}
