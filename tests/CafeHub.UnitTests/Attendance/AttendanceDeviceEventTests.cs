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
using Microsoft.AspNetCore.Mvc.Filters;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class AttendanceDeviceEventTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid DeviceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid StaffId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid ActorId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task AValidDeviceKeyAuthenticatesItsDeviceAndBranch()
        {
            var key = ConnectorKeys.Generate();
            var store = new FakeDeviceStore(Access(key, AttendanceDeviceStatus.Active));

            var result = await new AttendanceDeviceAuthenticator(store).AuthenticateAsync(key, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(DeviceId, result.Value.Id);
            Assert.Equal(BranchId, result.Value.BranchId);
        }

        [Fact]
        public async Task MalformedUnknownAndRevokedDeviceKeysAreRefusedUniformly()
        {
            var key = ConnectorKeys.Generate();
            var active = new FakeDeviceStore(Access(key, AttendanceDeviceStatus.Active));
            var revoked = new FakeDeviceStore(Access(key, AttendanceDeviceStatus.Revoked));
            var authenticator = new AttendanceDeviceAuthenticator(active);

            var refusals = new[]
            {
                await authenticator.AuthenticateAsync(null, default),
                await authenticator.AuthenticateAsync(string.Empty, default),
                await authenticator.AuthenticateAsync("not-a-device-key", default),
                await authenticator.AuthenticateAsync(new string('a', ConnectorKeys.KeyLength), default),
                await new AttendanceDeviceAuthenticator(revoked).AuthenticateAsync(key, default),
            };

            Assert.All(refusals, refusal =>
            {
                Assert.False(refusal.IsSuccess);
                Assert.Equal(AttendanceErrors.DeviceUnauthorized(), refusal.Error);
                Assert.Equal(ErrorType.Unauthorized, refusal.Error!.Type);
            });
        }

        [Fact]
        public void DeviceEventsAreAcceptedOnlyInsideTheProposedTimeWindow()
        {
            Assert.True(AttendanceDeviceEventPolicy.IsWithinWindow(Now.AddDays(-7), Now));
            Assert.True(AttendanceDeviceEventPolicy.IsWithinWindow(Now.AddMinutes(5), Now));
            Assert.False(AttendanceDeviceEventPolicy.IsWithinWindow(Now.AddDays(-7).AddSeconds(-1), Now));
            Assert.False(AttendanceDeviceEventPolicy.IsWithinWindow(Now.AddMinutes(5).AddSeconds(1), Now));
        }

        [Fact]
        public void AMatchedEventChecksInAndTheNextOneChecksOut()
        {
            var checkIn = AttendanceDeviceEventPolicy.Decide(Context());

            Assert.Equal(AttendanceDeviceEventPairing.CheckIn, checkIn.Pairing);
            Assert.Null(checkIn.RecordId);

            var open = AttendanceRecord.CheckInFromDevice(
                Guid.CreateVersion7(), BranchId, StaffId, Now.AddHours(-2), Now);
            var checkOut = AttendanceDeviceEventPolicy.Decide(Context(openRecord: open));

            Assert.Equal(AttendanceDeviceEventPairing.CheckOut, checkOut.Pairing);
            Assert.Equal(open.Id, checkOut.RecordId);
        }

        [Fact]
        public void UnusableMappingsStayUnmatchedAndAnOutOfOrderEventIsRejected()
        {
            var open = AttendanceRecord.CheckInFromDevice(
                Guid.CreateVersion7(), BranchId, StaffId, Now.AddHours(-2), Now);

            var inactive = AttendanceDeviceEventPolicy.Decide(Context(openRecord: open, active: false));
            var foreign = AttendanceDeviceEventPolicy.Decide(
                Context(openRecord: open, staffBranchId: OtherBranchId));
            var otherBranchRecord = AttendanceRecord.CheckInFromDevice(
                Guid.CreateVersion7(), OtherBranchId, StaffId, Now.AddHours(-2), Now);
            var otherBranch = AttendanceDeviceEventPolicy.Decide(
                Context(openRecord: otherBranchRecord));
            var outOfOrder = AttendanceDeviceEventPolicy.Decide(
                Context(openRecord: open, occurredAt: open.CheckInAt));

            Assert.Equal(AttendanceDeviceEventPairing.Unmatched, inactive.Pairing);
            Assert.Equal(AttendanceDeviceEventPairing.Unmatched, foreign.Pairing);
            Assert.Equal(AttendanceDeviceEventPairing.Unmatched, otherBranch.Pairing);
            Assert.Equal(AttendanceDeviceEventPairing.Rejected, outOfOrder.Pairing);
        }

        [Fact]
        public void ARepeatedEventIdIsADuplicateAndIsNeverPlannedForProcessing()
        {
            var events = new[]
            {
                Input("existing", "42", Now),
                Input("fresh", "42", Now),
                Input("fresh", "42", Now.AddMinutes(1)),
                Input("stale", "42", Now.AddDays(-8)),
            };
            var existing = new HashSet<string>(["existing"], StringComparer.Ordinal);

            var items = AttendanceDeviceEventBatch.Classify(events, existing, Now);

            Assert.True(items[0].IsDuplicate);
            Assert.False(items[1].IsDuplicate);
            Assert.True(items[2].IsDuplicate);
            Assert.False(items[3].IsDuplicate);
            Assert.True(items[3].IsOutsideWindow);
            Assert.Equal(1, items.Count(item => item.IsOutsideWindow));
            Assert.Equal(2, items.Count(item => item.IsDuplicate));
        }

        [Fact]
        public void DeviceRecordsPairWithoutASessionAndProcessOnce()
        {
            var record = AttendanceRecord.CheckInFromDevice(
                Guid.CreateVersion7(), BranchId, StaffId, Now.AddHours(-2), Now);

            Assert.Equal(AttendanceSource.Device, record.Source);
            Assert.Null(record.CheckInSessionId);
            Assert.True(record.IsOpen);
            Assert.False(record.CanCheckOutWith(Guid.NewGuid()));

            record.CheckOutFromDevice(Now.AddMinutes(-30), Now.AddMinutes(-29));

            Assert.False(record.IsOpen);
            Assert.Equal(Now.AddMinutes(-30), record.CheckOutAt);
            Assert.Null(record.CheckOutSessionId);
            Assert.Throws<InvalidOperationException>(
                () => record.CheckOutFromDevice(Now, Now));

            var deviceEvent = AttendanceDeviceEvent.Create(
                Guid.CreateVersion7(),
                DeviceId,
                "event-1",
                "42",
                Now,
                AttendanceDeviceEventDirection.In,
                Now);

            Assert.Equal(AttendanceDeviceEventStatus.Unmatched, deviceEvent.Status);
            Assert.Null(deviceEvent.RecordId);
            var recordId = Guid.CreateVersion7();
            Assert.True(deviceEvent.MarkProcessed(recordId, Now));
            Assert.Equal(recordId, deviceEvent.RecordId);
            Assert.Equal(AttendanceDeviceEventStatus.Processed, deviceEvent.Status);
            Assert.False(deviceEvent.MarkProcessed(recordId, Now));
        }

        [Fact]
        public async Task IngestionForwardsTheAuthenticatedDeviceScopeAndClock()
        {
            var store = new FakeEventStore();
            var handler = new IngestAttendanceDeviceEventsCommandHandler(store, new FixedClock(Now));
            var events = new[] { Input("event-1", "42", Now) };

            var result = await handler.Handle(
                new IngestAttendanceDeviceEventsCommand(DeviceId, BranchId, events), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(DeviceId, store.LastDeviceId);
            Assert.Equal(BranchId, store.LastBranchId);
            Assert.Equal(Now, store.LastNow);
            Assert.Same(events, store.LastEvents);
        }

        [Fact]
        public void IngestionValidatorEnforcesTheBatchLimitAndEventShape()
        {
            var validator = new IngestAttendanceDeviceEventsCommandValidator();

            Assert.True(validator.Validate(new IngestAttendanceDeviceEventsCommand(
                DeviceId, BranchId, [Input("event-1", "42", Now)])).IsValid);
            Assert.False(validator.Validate(new IngestAttendanceDeviceEventsCommand(
                DeviceId, BranchId, [])).IsValid);
            Assert.False(validator.Validate(new IngestAttendanceDeviceEventsCommand(
                DeviceId,
                BranchId,
                Enumerable.Range(0, 101).Select(index => Input($"event-{index}", "42", Now)).ToArray()))
                .IsValid);
            Assert.False(validator.Validate(new IngestAttendanceDeviceEventsCommand(
                DeviceId, BranchId, [Input(" ", "42", Now)])).IsValid);
            Assert.False(validator.Validate(new IngestAttendanceDeviceEventsCommand(
                DeviceId, BranchId, [Input("event-1", " ", Now)])).IsValid);
            Assert.False(validator.Validate(new IngestAttendanceDeviceEventsCommand(
                DeviceId, BranchId, [Input("event-1", "42", Now, "sideways")])).IsValid);
        }

        [Fact]
        public async Task ReprocessingIsScopedAndReturnsTheCounts()
        {
            var store = new FakeUnmatchedStore();
            var result = await new ReprocessUnmatchedAttendanceDeviceEventsCommandHandler(store, StaffUser())
                .Handle(new ReprocessUnmatchedAttendanceDeviceEventsCommand(BranchId), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(BranchId, store.LastReprocessBranchId);
            Assert.Equal(ActorId, store.LastActorId);
            Assert.Equal(2, result.Value.Processed);
            Assert.Equal(1, result.Value.Unmatched);
            Assert.Equal(0, result.Value.Rejected);

            var foreign = await new ReprocessUnmatchedAttendanceDeviceEventsCommandHandler(
                    store, StaffUser(OtherBranchId))
                .Handle(new ReprocessUnmatchedAttendanceDeviceEventsCommand(BranchId), default);
            Assert.Equal(AttendanceErrors.BranchNotFound(), foreign.Error);
        }

        [Fact]
        public async Task TheUnmatchedListRequiresABranchAndPinsStaffToTheirOwn()
        {
            var store = new FakeUnmatchedStore();
            var missing = await new GetUnmatchedAttendanceDeviceEventsQueryHandler(store, OwnerUser())
                .Handle(new GetUnmatchedAttendanceDeviceEventsQuery(null), default);
            Assert.Equal(AttendanceErrors.BranchRequired(), missing.Error);

            var pinned = await new GetUnmatchedAttendanceDeviceEventsQueryHandler(store, StaffUser())
                .Handle(new GetUnmatchedAttendanceDeviceEventsQuery(null), default);
            Assert.True(pinned.IsSuccess, pinned.Error?.Code);
            Assert.Equal(BranchId, store.LastListBranchId);

            var foreign = await new GetUnmatchedAttendanceDeviceEventsQueryHandler(store, StaffUser())
                .Handle(new GetUnmatchedAttendanceDeviceEventsQuery(OtherBranchId), default);
            Assert.Equal(AttendanceErrors.BranchNotFound(), foreign.Error);
        }

        [Fact]
        public void TheReprocessAuditHoldsTheCountsButNeverADeviceKey()
        {
            var key = ConnectorKeys.Generate();
            var snapshot = TenantAuditSnapshotSerializer.Create(
                "attendance_device_events",
                TenantAuditChanges.Create(
                [
                    new("Processed", null, "2"),
                    new("Unmatched", null, "1"),
                    new("Rejected", null, "0"),
                    new("BranchId", null, BranchId.ToString()),
                ]));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"Processed\":\"2\"", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("KeyHash", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain(key, afterState, StringComparison.Ordinal);
            Assert.DoesNotContain(ConnectorKeys.Hash(key), afterState, StringComparison.Ordinal);
        }

        [Fact]
        public void TheConnectorEndpointIsAnonymousAndUsesTheDeviceKeyFilter()
        {
            var controller = typeof(TenantAttendanceConnectorController);
            Assert.Equal(
                "t/{slug}/api/v1/connector/attendance",
                controller.GetCustomAttribute<RouteAttribute>()!.Template);
            Assert.NotNull(controller.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(controller.GetCustomAttribute<TenantAuthorizeAttribute>());
            var filter = Assert.IsType<ServiceFilterAttribute>(
                controller.GetCustomAttribute<ServiceFilterAttribute>());
            Assert.Equal(typeof(AttendanceDeviceKeyAuthorizationFilter), filter.ServiceType);

            var action = controller.GetMethod(nameof(TenantAttendanceConnectorController.Events))!;
            Assert.Equal("events", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Null(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal("X-Connector-Key", AttendanceDeviceKeyAuthorizationFilter.HeaderName);
        }

        [Fact]
        public void TheUnmatchedEndpointsAreProtectedByAttendanceManage()
        {
            var get = typeof(TenantAttendanceController)
                .GetMethod(nameof(TenantAttendanceController.Unmatched))!;
            Assert.Equal("unmatched", get.GetCustomAttribute<HttpGetAttribute>()!.Template);
            Assert.Equal(
                TenantPermissions.AttendanceManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    get.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);

            var post = typeof(TenantAttendanceController)
                .GetMethod(nameof(TenantAttendanceController.ReprocessUnmatched))!;
            Assert.Equal(
                "unmatched/reprocess",
                post.GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Equal(
                TenantPermissions.AttendanceManage,
                Assert.IsType<TenantAuthorizeAttribute>(
                    post.GetCustomAttribute<TenantAuthorizeAttribute>()).Policy);
            Assert.Null(post.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public async Task TheUnmatchedActionsSendTheirRequests()
        {
            var sender = new RecordingSender();
            var controller = CreateAttendanceController(sender);

            sender.Response = Result.Success<IReadOnlyList<AttendanceUnmatchedEventResponse>>([]);
            var listed = await controller.Unmatched(BranchId, default);
            Assert.IsType<OkObjectResult>(listed.Result);
            Assert.Equal(
                BranchId,
                Assert.IsType<GetUnmatchedAttendanceDeviceEventsQuery>(sender.LastRequest).BranchId);

            sender.Response = Result.Success(new AttendanceDeviceReprocessResponse(1, 0, 0));
            var reprocessed = await controller.ReprocessUnmatched(
                new AttendanceDeviceScopeRequest(BranchId), default);
            Assert.IsType<OkObjectResult>(reprocessed.Result);
            Assert.Equal(
                BranchId,
                Assert.IsType<ReprocessUnmatchedAttendanceDeviceEventsCommand>(sender.LastRequest)
                    .BranchId);
        }

        [Fact]
        public async Task TheConnectorSendsTheAuthenticatedDeviceAndEvents()
        {
            var sender = new RecordingSender
            {
                Response = Result.Success(new AttendanceDeviceEventsResponse(
                    [new AttendanceDeviceEventOutcome("event-1", AttendanceDeviceEventOutcomes.Processed, null, null)])),
            };
            var httpContext = new DefaultHttpContext();
            httpContext.SetAttendanceDevice(new AttendanceDeviceAccess(
                DeviceId,
                BranchId,
                "Front door",
                ConnectorKeys.Hash(ConnectorKeys.Generate()),
                AttendanceDeviceStatus.Active));
            var controller = new TenantAttendanceConnectorController(
                sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
            };

            var result = await controller.Events(
                new AttendanceDeviceEventsRequest(
                    [new AttendanceDeviceEventRequest("event-1", "42", Now, "In")]),
                default);

            Assert.IsType<OkObjectResult>(result.Result);
            var command = Assert.IsType<IngestAttendanceDeviceEventsCommand>(sender.LastRequest);
            Assert.Equal(DeviceId, command.DeviceId);
            Assert.Equal(BranchId, command.BranchId);
            var input = Assert.Single(command.Events!);
            Assert.Equal("event-1", input.EventId);
            Assert.Equal("42", input.Code);
            Assert.Equal(Now, input.OccurredAt);
            Assert.Equal("In", input.Direction);
        }

        private static AttendanceDeviceEventContext Context(
            AttendanceRecord? openRecord = null,
            bool active = true,
            Guid? staffBranchId = null,
            DateTimeOffset? occurredAt = null) =>
            new(
                BranchId,
                StaffId,
                staffBranchId ?? BranchId,
                active,
                openRecord,
                occurredAt ?? Now);

        private static AttendanceDeviceEventInput Input(
            string eventId,
            string code,
            DateTimeOffset occurredAt,
            string? direction = null) =>
            new(eventId, code, occurredAt, direction);

        private static AttendanceDeviceAccess Access(string key, AttendanceDeviceStatus status) =>
            new(DeviceId, BranchId, "Front door", ConnectorKeys.Hash(key), status);

        private static FakeTenantUser OwnerUser() =>
            new(new TenantAccountAccess(false, "1", false));

        private static FakeTenantUser StaffUser(Guid? branchId = null) =>
            new(new TenantAccountAccess(true, "1", true) { BranchId = branchId ?? BranchId });

        private static TenantAttendanceController CreateAttendanceController(ISender sender) =>
            new(sender, new TestCorrelationIdAccessor())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-device-events" },
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

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakeDeviceStore(AttendanceDeviceAccess? access) : IAttendanceDeviceStore
        {
            public Task<AttendanceDeviceAccess?> FindByKeyHashAsync(
                string keyHash,
                CancellationToken cancellationToken) =>
                Task.FromResult(access is not null && access.KeyHash == keyHash ? access : null);

            public Task<Result<AttendanceDeviceResponse>> CreateAsync(
                Guid branchId,
                string name,
                string keyHash,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<IReadOnlyList<AttendanceDeviceResponse>> ListAsync(
                Guid branchId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Result<AttendanceDeviceResponse>> RevokeAsync(
                Guid deviceId,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Result<AttendanceDeviceCodeResponse>> MapCodeAsync(
                Guid branchId,
                Guid staffId,
                string deviceCode,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Result> UnmapCodeAsync(
                Guid branchId,
                Guid staffId,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }

        private sealed class FakeEventStore : IAttendanceDeviceEventStore
        {
            public Guid? LastDeviceId { get; private set; }

            public Guid? LastBranchId { get; private set; }

            public IReadOnlyList<AttendanceDeviceEventInput>? LastEvents { get; private set; }

            public DateTimeOffset? LastNow { get; private set; }

            public Task<Result<AttendanceDeviceEventsResponse>> IngestAsync(
                Guid deviceId,
                Guid branchId,
                IReadOnlyList<AttendanceDeviceEventInput> events,
                DateTimeOffset now,
                CancellationToken cancellationToken)
            {
                LastDeviceId = deviceId;
                LastBranchId = branchId;
                LastEvents = events;
                LastNow = now;
                return Task.FromResult(Result.Success(new AttendanceDeviceEventsResponse([])));
            }
        }

        private sealed class FakeUnmatchedStore : IAttendanceUnmatchedEventStore
        {
            public Guid? LastListBranchId { get; private set; }

            public Guid? LastReprocessBranchId { get; private set; }

            public Guid? LastActorId { get; private set; }

            public Task<IReadOnlyList<AttendanceUnmatchedEventResponse>> ListAsync(
                Guid branchId,
                CancellationToken cancellationToken)
            {
                LastListBranchId = branchId;
                return Task.FromResult<IReadOnlyList<AttendanceUnmatchedEventResponse>>([]);
            }

            public Task<Result<AttendanceDeviceReprocessResponse>> ReprocessAsync(
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                LastReprocessBranchId = branchId;
                LastActorId = actorUserId;
                return Task.FromResult(Result.Success(
                    new AttendanceDeviceReprocessResponse(2, 1, 0)));
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
            public string? CorrelationId => "correlation-device-events";
        }
    }
}
