using System.Reflection;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Attendance.Queries;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class AttendanceDeviceTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid DeviceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid StaffId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid ActorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task TheDeviceKeyIsReturnedOnceAndOnlyItsHashIsStored()
        {
            var store = new FakeDeviceStore();
            var handler = new CreateAttendanceDeviceCommandHandler(store, StaffUser());

            var result = await handler.Handle(
                new CreateAttendanceDeviceCommand(BranchId, "Front door"), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var created = result.Value;
            Assert.True(ConnectorKeys.IsWellFormed(created.Key));
            Assert.Equal(ConnectorKeys.Hash(created.Key), store.LastKeyHash);
            Assert.NotEqual(created.Key, store.LastKeyHash);
            Assert.Equal(BranchId, store.LastBranchId);
            Assert.Equal(ActorId, store.LastActorId);
            Assert.DoesNotContain(
                "KeyHash",
                typeof(AttendanceDeviceCreatedResponse).GetProperties()
                    .Select(property => property.Name));
        }

        [Fact]
        public void DeviceKeysAreRandomAndTheDomainAcceptsOnlyAHash()
        {
            var first = ConnectorKeys.Generate();
            var second = ConnectorKeys.Generate();

            Assert.NotEqual(first, second);
            Assert.True(ConnectorKeys.IsWellFormed(first));
            Assert.NotEqual(first, ConnectorKeys.Hash(first));
            Assert.True(AttendanceDevice.IsValidKeyHash(ConnectorKeys.Hash(first)));
            Assert.False(AttendanceDevice.IsValidKeyHash(null));
            Assert.True(AttendanceDevice.IsValidName("Front door"));
            Assert.False(AttendanceDevice.IsValidName(" "));
            Assert.False(AttendanceDevice.IsValidName(new string('a', 65)));

            var device = AttendanceDevice.Create(
                DeviceId,
                BranchId,
                "Front door",
                ConnectorKeys.Hash(first),
                Now,
                ActorId);

            Assert.Equal(AttendanceDeviceStatus.Active, device.Status);
            Assert.True(device.Revoke(Now, ActorId));
            Assert.Equal(AttendanceDeviceStatus.Revoked, device.Status);
            Assert.False(device.Revoke(Now, ActorId));
        }

        [Fact]
        public void TheDeviceAuditNeverHoldsTheKeyOrItsHash()
        {
            var key = ConnectorKeys.Generate();
            var snapshot = TenantAuditSnapshotSerializer.Create(
                "attendance_devices",
                TenantAuditChanges.Create(
                [
                    new("Name", null, "Front door"),
                    new("Status", null, AttendanceDeviceStatus.Active.ToString()),
                    new("BranchId", null, BranchId.ToString()),
                ]));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"Name\":\"Front door\"", afterState, StringComparison.Ordinal);
            Assert.Contains("\"Status\":\"Active\"", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("KeyHash", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain(key, afterState, StringComparison.Ordinal);
            Assert.DoesNotContain(ConnectorKeys.Hash(key), afterState, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnotherBranchesStaffCannotCreateRevokeOrMapADevice()
        {
            var store = new FakeDeviceStore();
            var user = StaffUser(OtherBranchId);

            var created = await new CreateAttendanceDeviceCommandHandler(store, user)
                .Handle(new CreateAttendanceDeviceCommand(BranchId, "Front door"), default);
            var revoked = await new RevokeAttendanceDeviceCommandHandler(store, user)
                .Handle(new RevokeAttendanceDeviceCommand(DeviceId, BranchId), default);
            var mapped = await new MapAttendanceDeviceCodeCommandHandler(store, user)
                .Handle(new MapAttendanceDeviceCodeCommand(BranchId, StaffId, "42"), default);
            var unmapped = await new UnmapAttendanceDeviceCodeCommandHandler(store, user)
                .Handle(new UnmapAttendanceDeviceCodeCommand(BranchId, StaffId), default);

            Assert.Equal(AttendanceErrors.BranchNotFound(), created.Error);
            Assert.Equal(AttendanceErrors.BranchNotFound(), revoked.Error);
            Assert.Equal(AttendanceErrors.BranchNotFound(), mapped.Error);
            Assert.Equal(AttendanceErrors.BranchNotFound(), unmapped.Error);
            Assert.Null(store.LastKeyHash);
            Assert.Null(store.LastDeviceId);
            Assert.Null(store.LastDeviceCode);
        }

        [Fact]
        public async Task MappingAndUnmappingForwardTheScopedBranchTheStaffAndTheCode()
        {
            var store = new FakeDeviceStore();
            var handler = new MapAttendanceDeviceCodeCommandHandler(store, StaffUser());

            var mapped = await handler.Handle(
                new MapAttendanceDeviceCodeCommand(BranchId, StaffId, " 42 "), default);

            Assert.True(mapped.IsSuccess, mapped.Error?.Code);
            Assert.Equal(BranchId, store.LastBranchId);
            Assert.Equal(StaffId, store.LastStaffId);
            Assert.Equal(" 42 ", store.LastDeviceCode);
            Assert.Equal(ActorId, store.LastActorId);

            var unmapped = await new UnmapAttendanceDeviceCodeCommandHandler(store, StaffUser())
                .Handle(new UnmapAttendanceDeviceCodeCommand(BranchId, StaffId), default);

            Assert.True(unmapped.IsSuccess, unmapped.Error?.Code);
            Assert.Equal(StaffId, store.LastUnmappedStaffId);
        }

        [Fact]
        public void AStaffMemberHasOneCodePerBranchAndOneCodeBelongsToOneStaffMember()
        {
            var code = AttendanceDeviceCode.Create(
                Guid.CreateVersion7(), BranchId, StaffId, "42", Now, ActorId);

            Assert.Equal("42", code.DeviceCode);
            Assert.False(code.ChangeCode(" 42 ", Now, ActorId));
            Assert.True(code.ChangeCode("43", Now, ActorId));
            Assert.Equal("43", code.DeviceCode);
            Assert.True(AttendanceDeviceCode.IsValidCode(" 42 "));
            Assert.False(AttendanceDeviceCode.IsValidCode(null));
            Assert.False(AttendanceDeviceCode.IsValidCode(" "));
            Assert.False(AttendanceDeviceCode.IsValidCode(new string('a', 65)));
            Assert.False(AttendanceDeviceCode.IsValidCode("4\u00072"));
        }

        [Fact]
        public void DeviceValidatorsRequireTheNameBranchStaffAndCode()
        {
            var create = new CreateAttendanceDeviceCommandValidator();
            Assert.True(create.Validate(new CreateAttendanceDeviceCommand(BranchId, "Front door")).IsValid);
            Assert.False(create.Validate(new CreateAttendanceDeviceCommand(Guid.Empty, "Front door")).IsValid);
            Assert.False(create.Validate(new CreateAttendanceDeviceCommand(BranchId, " ")).IsValid);

            var revoke = new RevokeAttendanceDeviceCommandValidator();
            Assert.True(revoke.Validate(new RevokeAttendanceDeviceCommand(DeviceId, BranchId)).IsValid);
            Assert.False(revoke.Validate(new RevokeAttendanceDeviceCommand(Guid.Empty, BranchId)).IsValid);

            var map = new MapAttendanceDeviceCodeCommandValidator();
            Assert.True(map.Validate(
                new MapAttendanceDeviceCodeCommand(BranchId, StaffId, "42")).IsValid);
            Assert.False(map.Validate(
                new MapAttendanceDeviceCodeCommand(BranchId, Guid.Empty, "42")).IsValid);
            Assert.False(map.Validate(
                new MapAttendanceDeviceCodeCommand(BranchId, StaffId, " ")).IsValid);

            var unmap = new UnmapAttendanceDeviceCodeCommandValidator();
            Assert.True(unmap.Validate(new UnmapAttendanceDeviceCodeCommand(BranchId, StaffId)).IsValid);
            Assert.False(unmap.Validate(
                new UnmapAttendanceDeviceCodeCommand(BranchId, Guid.Empty)).IsValid);
        }

        [Fact]
        public async Task TheDeviceListRequiresABranchAndPinsStaffToTheirOwn()
        {
            var store = new FakeDeviceStore();
            var owner = new GetAttendanceDevicesQueryHandler(store, OwnerUser());

            var missing = await owner.Handle(new GetAttendanceDevicesQuery(null), default);
            Assert.Equal(AttendanceErrors.BranchRequired(), missing.Error);

            var pinned = await new GetAttendanceDevicesQueryHandler(store, StaffUser())
                .Handle(new GetAttendanceDevicesQuery(null), default);
            Assert.True(pinned.IsSuccess, pinned.Error?.Code);
            Assert.Equal(BranchId, store.LastListBranchId);

            var foreign = await new GetAttendanceDevicesQueryHandler(store, StaffUser())
                .Handle(new GetAttendanceDevicesQuery(OtherBranchId), default);
            Assert.Equal(AttendanceErrors.BranchNotFound(), foreign.Error);
        }

        [Fact]
        public void TheDeviceEndpointsAreProtectedByAttendanceManage()
        {
            var controller = typeof(TenantAttendanceDevicesController);
            Assert.Equal(
                "t/{slug}/api/v1/attendance/devices",
                controller.GetCustomAttribute<RouteAttribute>()!.Template);

            foreach (var actionName in new[]
            {
                nameof(TenantAttendanceDevicesController.List),
                nameof(TenantAttendanceDevicesController.Create),
                nameof(TenantAttendanceDevicesController.Revoke),
                nameof(TenantAttendanceDevicesController.MapCode),
                nameof(TenantAttendanceDevicesController.UnmapCode),
            })
            {
                var action = controller.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(action);
                var authorize = Assert.IsType<TenantAuthorizeAttribute>(
                    action!.GetCustomAttribute<TenantAuthorizeAttribute>());
                Assert.Equal(TenantPermissions.AttendanceManage, authorize.Policy);
                Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            }
        }

        [Fact]
        public async Task TheDeviceActionsSendTheirCommandsAndReturnTheirResults()
        {
            var device = new AttendanceDeviceResponse(
                DeviceId, BranchId, "Front door", AttendanceDeviceStatus.Active.ToString(), null, Now);
            var sender = new RecordingSender();
            var controller = CreateController(sender);

            sender.Response = Result.Success(new AttendanceDeviceCreatedResponse(
                DeviceId,
                BranchId,
                "Front door",
                AttendanceDeviceStatus.Active.ToString(),
                Now,
                ConnectorKeys.Generate()));
            var created = await controller.Create(
                "cafe", new CreateAttendanceDeviceRequest(BranchId, "Front door"), default);
            Assert.IsType<CreatedAtActionResult>(created.Result);
            Assert.IsType<CreateAttendanceDeviceCommand>(sender.LastRequest);

            sender.Response = Result.Success<IReadOnlyList<AttendanceDeviceResponse>>([device]);
            var listed = await controller.List(BranchId, default);
            Assert.IsType<OkObjectResult>(listed.Result);
            Assert.IsType<GetAttendanceDevicesQuery>(sender.LastRequest);

            sender.Response = Result.Success(device);
            var revoked = await controller.Revoke(
                DeviceId, new AttendanceDeviceScopeRequest(BranchId), default);
            Assert.IsType<OkObjectResult>(revoked.Result);
            Assert.IsType<RevokeAttendanceDeviceCommand>(sender.LastRequest);

            sender.Response = Result.Success(new AttendanceDeviceCodeResponse(BranchId, StaffId, "42"));
            var mapped = await controller.MapCode(
                new MapAttendanceDeviceCodeRequest(BranchId, StaffId, "42"), default);
            Assert.IsType<OkObjectResult>(mapped.Result);
            Assert.IsType<MapAttendanceDeviceCodeCommand>(sender.LastRequest);

            sender.Response = Result.Success();
            var unmapped = await controller.UnmapCode(StaffId, BranchId, default);
            Assert.IsType<NoContentResult>(unmapped);
            Assert.IsType<UnmapAttendanceDeviceCodeCommand>(sender.LastRequest);
        }

        private static FakeTenantUser OwnerUser() =>
            new(new TenantAccountAccess(false, "1", false));

        private static FakeTenantUser StaffUser(Guid? branchId = null) =>
            new(new TenantAccountAccess(true, "1", true) { BranchId = branchId ?? BranchId });

        private static TenantAttendanceDevicesController CreateController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-attendance-devices" },
                },
            };

        private sealed class FakeTenantUser(TenantAccountAccess accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => ActorId;

            public Guid? TenantId => Guid.Parse("66666666-6666-6666-6666-666666666666");

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => accountAccess.BranchId;

            public TenantAccountAccess? AccountAccess => accountAccess;
        }

        private sealed class FakeDeviceStore : IAttendanceDeviceStore
        {
            public string? LastKeyHash { get; private set; }

            public Guid? LastBranchId { get; private set; }

            public Guid? LastActorId { get; private set; }

            public Guid? LastDeviceId { get; private set; }

            public Guid? LastStaffId { get; private set; }

            public Guid? LastUnmappedStaffId { get; private set; }

            public string? LastDeviceCode { get; private set; }

            public Guid? LastListBranchId { get; private set; }

            public Task<AttendanceDeviceAccess?> FindByKeyHashAsync(
                string keyHash,
                CancellationToken cancellationToken) =>
                Task.FromResult<AttendanceDeviceAccess?>(null);

            public Task<Result<AttendanceDeviceResponse>> CreateAsync(
                Guid branchId,
                string name,
                string keyHash,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                LastBranchId = branchId;
                LastKeyHash = keyHash;
                LastActorId = actorUserId;
                return Task.FromResult(Result.Success(new AttendanceDeviceResponse(
                    DeviceId,
                    branchId,
                    name.Trim(),
                    AttendanceDeviceStatus.Active.ToString(),
                    null,
                    Now)));
            }

            public Task<IReadOnlyList<AttendanceDeviceResponse>> ListAsync(
                Guid branchId,
                CancellationToken cancellationToken)
            {
                LastListBranchId = branchId;
                return Task.FromResult<IReadOnlyList<AttendanceDeviceResponse>>([]);
            }

            public Task<Result<AttendanceDeviceResponse>> RevokeAsync(
                Guid deviceId,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                LastDeviceId = deviceId;
                LastBranchId = branchId;
                LastActorId = actorUserId;
                return Task.FromResult(Result.Success(new AttendanceDeviceResponse(
                    deviceId,
                    branchId,
                    "Front door",
                    AttendanceDeviceStatus.Revoked.ToString(),
                    null,
                    Now)));
            }

            public Task<Result<AttendanceDeviceCodeResponse>> MapCodeAsync(
                Guid branchId,
                Guid staffId,
                string deviceCode,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                LastBranchId = branchId;
                LastStaffId = staffId;
                LastDeviceCode = deviceCode;
                LastActorId = actorUserId;
                return Task.FromResult(Result.Success(
                    new AttendanceDeviceCodeResponse(branchId, staffId, deviceCode.Trim())));
            }

            public Task<Result> UnmapCodeAsync(
                Guid branchId,
                Guid staffId,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                LastBranchId = branchId;
                LastUnmappedStaffId = staffId;
                LastActorId = actorUserId;
                return Task.FromResult(Result.Success());
            }
        }

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

        private sealed class TestCorrelationIdAccessor
            : CafeHub.Application.Abstractions.Correlation.ICorrelationIdAccessor
        {
            public string? CorrelationId => "correlation-attendance-devices";
        }
    }
}
