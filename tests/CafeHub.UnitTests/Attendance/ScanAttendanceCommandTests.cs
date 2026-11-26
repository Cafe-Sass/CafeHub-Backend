using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class ScanAttendanceCommandTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid StaffId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid TestBranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
        private static readonly string Token = new('a', AttendanceQrSession.TokenLength);

        [Fact]
        public async Task ScanHandler_UsesTheAccountIdentityAndBranchAndForwardsTheToken()
        {
            var store = new FakeRecordStore();
            var handler = new ScanAttendanceCommandHandler(store, User());

            var result = await handler.Handle(new ScanAttendanceCommand(Token), default);

            Assert.True(result.IsSuccess);
            Assert.Equal((StaffId, TestBranchId, Token), store.Call);
        }

        [Fact]
        public async Task ScanHandler_RefusesAMissingActor()
        {
            var store = new FakeRecordStore();
            var handler = new ScanAttendanceCommandHandler(
                store, new FakeTenantUser { UserId = null });

            var result = await handler.Handle(new ScanAttendanceCommand(Token), default);

            Assert.Equal(AuthErrors.Forbidden(), result.Error);
            Assert.Null(store.Call);
        }

        [Fact]
        public async Task ScanHandler_RefusesAUserWithoutAnActiveAssignedBranch()
        {
            var store = new FakeRecordStore();
            var handler = new ScanAttendanceCommandHandler(
                store,
                new FakeTenantUser { AccountAccess = new TenantAccountAccess(false, "1", false) });

            var result = await handler.Handle(new ScanAttendanceCommand(Token), default);

            Assert.Equal(AuthErrors.Forbidden(), result.Error);
            Assert.Null(store.Call);
        }

        [Fact]
        public void ScanValidator_RefusesAMalformedToken()
        {
            var validator = new ScanAttendanceCommandValidator();

            Assert.True(validator.Validate(new ScanAttendanceCommand(Token)).IsValid);

            var invalid = validator.Validate(new ScanAttendanceCommand("short"));
            Assert.False(invalid.IsValid);
            Assert.Equal("Attendance.InvalidToken", invalid.Errors[0].ErrorCode);
        }

        private static FakeTenantUser User() => new();

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = StaffId;

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Barista" };

            public Guid? BranchId { get; init; } = TestBranchId;

            public TenantAccountAccess? AccountAccess { get; init; } =
                new(true, "1", true) { BranchId = TestBranchId };
        }

        private sealed class FakeRecordStore : IAttendanceRecordStore
        {
            public (Guid ActorUserId, Guid StaffBranchId, string Token)? Call { get; private set; }

            public Task<Result<AttendanceScanResponse>> ScanAsync(
                Guid actorUserId,
                Guid staffBranchId,
                string token,
                CancellationToken cancellationToken)
            {
                Call = (actorUserId, staffBranchId, token);
                return Task.FromResult(Result.Success(new AttendanceScanResponse(
                    Guid.CreateVersion7(),
                    AttendanceScanResponse.CheckedIn,
                    staffBranchId,
                    Now)));
            }
        }
    }
}
