using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Attendance.Queries;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class TenantAttendanceControllerTests
    {
        private const string Route = "t/{slug}/api/v1/attendance";
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Controller_IsAnApiControllerOnTheAttendanceRoute()
        {
            Assert.NotNull(typeof(TenantAttendanceController).GetCustomAttribute<ApiControllerAttribute>());
            var route = typeof(TenantAttendanceController).GetCustomAttribute<RouteAttribute>();
            Assert.NotNull(route);
            Assert.Equal(Route, route!.Template);
        }

        [Fact]
        public void SettingsActions_AreTheDocumentedVerbsProtectedByAttendanceManage()
        {
            var get = Action(nameof(TenantAttendanceController.GetSettings));
            Assert.Equal("settings", get.GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal(
                TenantPermissions.AttendanceManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    get.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);

            var put = Action(nameof(TenantAttendanceController.UpdateSettings));
            Assert.Equal("settings", put.GetCustomAttribute<HttpPutAttribute>()!.Template);
            Assert.Equal(
                TenantPermissions.AttendanceManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    put.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            Assert.NotNull(put.GetParameters().Single(parameter => parameter.Name == "request"));
        }

        [Fact]
        public void IssueQr_IsABranchPostProtectedByAttendanceQrManage()
        {
            var action = Action(nameof(TenantAttendanceController.IssueQr));

            Assert.Equal("branches/{branchId:guid}/qr", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Equal(
                TenantPermissions.AttendanceQrManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    action.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            Assert.Null(action.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
        }

        [Fact]
        public async Task GetSettings_SendsTheSettingsQueryAndReturnsTheMode()
        {
            var response = new AttendanceSettingsResponse("Qr", "3");
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.GetSettings(CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            Assert.IsType<GetAttendanceSettingsQuery>(sender.LastRequest);
        }

        [Fact]
        public async Task UpdateSettings_ParsesTheModeAndForwardsTheToken()
        {
            var response = new AttendanceSettingsResponse("Qr", "4");
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.UpdateSettings(
                new UpdateAttendanceSettingsRequest("qr", "3"), CancellationToken.None);

            Assert.IsType<OkObjectResult>(actionResult.Result);
            var command = Assert.IsType<UpdateAttendanceSettingsCommand>(sender.LastRequest);
            Assert.Equal(AttendanceMode.Qr, command.Mode);
            Assert.Equal("3", command.ConcurrencyToken);
        }

        [Fact]
        public async Task UpdateSettings_WhenTheModeIsUnknown_ReturnsTheInvalidModeValidationError()
        {
            var sender = new RecordingSender();
            var controller = CreateController(sender);

            var actionResult = await controller.UpdateSettings(
                new UpdateAttendanceSettingsRequest("sometimes", "3"), CancellationToken.None);

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Attendance.InvalidMode", problem.Extensions["code"]);
            Assert.Null(sender.LastRequest);
        }

        [Fact]
        public async Task IssueQr_ForwardsTheRouteBranchAndReturnsTheTokenWindow()
        {
            var response = new AttendanceQrSessionResponse(
                BranchId, new string('a', AttendanceQrSession.TokenLength), Now, Now.AddMinutes(10));
            var sender = new RecordingSender { Response = Result.Success(response) };
            var controller = CreateController(sender);

            var actionResult = await controller.IssueQr(BranchId, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            Assert.Same(response, okResult.Value);
            Assert.Equal(
                BranchId, Assert.IsType<IssueAttendanceQrCommand>(sender.LastRequest).BranchId);
        }

        [Fact]
        public void TheAttendancePermissionsJoinTheCatalogAndStayGrantableToStaff()
        {
            Assert.Equal("attendance.manage", TenantPermissions.AttendanceManage);
            Assert.Equal("attendance.qr.manage", TenantPermissions.AttendanceQrManage);
            Assert.Equal("attendance.view", TenantPermissions.AttendanceView);

            foreach (var permission in new[]
            {
                TenantPermissions.AttendanceManage,
                TenantPermissions.AttendanceQrManage,
                TenantPermissions.AttendanceView,
            })
            {
                Assert.Contains(permission, TenantPermissions.All);
                Assert.True(TenantPermissions.IsStaffGrant(permission));
                Assert.False(TenantPermissions.IsOwnerOnly(permission));
            }
        }

        private static MethodInfo Action(string name) =>
            typeof(TenantAttendanceController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The TenantAttendanceController.{name} action was not found.");

        private static TenantAttendanceController CreateController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-attendance" },
                },
            };

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; set; }

            public object? LastRequest { get; private set; }

            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException("The controller must send a request with a response.");

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller must send a typed request.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");

            public IAsyncEnumerable<object?> CreateStream(
                object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The controller does not create streams.");
        }

        private sealed class TestCorrelationIdAccessor : CafeHub.Application.Abstractions.Correlation.ICorrelationIdAccessor
        {
            public string? CorrelationId => "correlation-attendance";
        }
    }
}
