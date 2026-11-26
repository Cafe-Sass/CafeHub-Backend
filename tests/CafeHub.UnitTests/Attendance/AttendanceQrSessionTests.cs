using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Attendance;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class AttendanceQrSessionTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        private static readonly string Token = new('a', AttendanceQrSession.TokenLength);
        private static readonly string OtherToken = new('b', AttendanceQrSession.TokenLength);

        [Fact]
        public void QrSession_IsValidOnlyInsideItsTenMinuteWindow()
        {
            var session = AttendanceQrSession.Create(
                Guid.CreateVersion7(), BranchId, Token, Now, ActorId);

            Assert.Equal(Now, session.ValidFrom);
            Assert.Equal(Now.AddMinutes(10), session.ValidTo);
            Assert.Equal(AttendanceQrSession.ExpiryFor(Now), session.ValidTo);
            Assert.True(session.IsValid(Now));
            Assert.True(session.IsValid(Now.AddMinutes(9)));
            Assert.False(session.IsValid(Now.AddMinutes(10)));
            Assert.False(session.IsValid(Now.AddMinutes(-1)));
        }

        [Fact]
        public void QrToken_MustBeFortyThreeUrlSafeCharacters()
        {
            Assert.True(AttendanceQrSession.IsValidToken(Token));
            Assert.True(AttendanceQrSession.IsValidToken(new string('a', 41) + "-_"));
            Assert.False(AttendanceQrSession.IsValidToken(null));
            Assert.False(AttendanceQrSession.IsValidToken(new string('a', 42)));
            Assert.False(AttendanceQrSession.IsValidToken(new string('a', 44)));
            Assert.False(AttendanceQrSession.IsValidToken(new string('a', 42) + "+"));
            Assert.False(AttendanceQrSession.IsValidToken(new string('a', 42) + "="));

            Assert.Throws<ArgumentException>(() => AttendanceQrSession.Create(
                Guid.CreateVersion7(), BranchId, "short", Now, ActorId));
            Assert.Throws<ArgumentException>(() => AttendanceQrSession.Create(
                Guid.CreateVersion7(), Guid.Empty, Token, Now, ActorId));
            Assert.Throws<ArgumentException>(() => AttendanceQrSession.Create(
                Guid.CreateVersion7(), BranchId, Token, Now, Guid.Empty));
        }

        [Fact]
        public void Current_ReusesAStillValidSessionAndFindsNoneAfterExpiry()
        {
            var expired = AttendanceQrSession.Create(
                Guid.CreateVersion7(), BranchId, Token, Now.AddMinutes(-20), ActorId);
            var valid = AttendanceQrSession.Create(
                Guid.CreateVersion7(), BranchId, OtherToken, Now.AddMinutes(-1), ActorId);

            Assert.Same(valid, AttendanceQrSession.Current(new[] { expired, valid }, Now));
            Assert.Same(valid, AttendanceQrSession.Current(new[] { valid, expired }, Now));
            Assert.Null(AttendanceQrSession.Current(new[] { expired }, Now));
            Assert.Null(AttendanceQrSession.Current(Array.Empty<AttendanceQrSession>(), Now));
        }

        [Fact]
        public void Current_PicksTheLatestWindowWhenTwoSessionsOverlap()
        {
            var earlier = AttendanceQrSession.Create(
                Guid.CreateVersion7(), BranchId, Token, Now.AddMinutes(-2), ActorId);
            var later = AttendanceQrSession.Create(
                Guid.CreateVersion7(), BranchId, OtherToken, Now.AddMinutes(-1), ActorId);

            Assert.Same(later, AttendanceQrSession.Current(new[] { earlier, later }, Now));
        }

        [Fact]
        public void QrTokenGenerator_ProducesUniqueValidTokens()
        {
            var generator = new AttendanceQrTokenGenerator();

            var first = generator.Generate();
            var second = generator.Generate();

            Assert.True(AttendanceQrSession.IsValidToken(first));
            Assert.True(AttendanceQrSession.IsValidToken(second));
            Assert.NotEqual(first, second);
        }

        [Fact]
        public async Task IssueHandler_IsRefusedOutsideQrMode()
        {
            foreach (var mode in new[] { AttendanceMode.Off, AttendanceMode.Device })
            {
                var store = new FakeQrStore();
                var handler = new IssueAttendanceQrCommandHandler(
                    store, new FakeSettingsReader { Mode = mode.ToString() }, User());

                var result = await handler.Handle(new IssueAttendanceQrCommand(BranchId), default);

                Assert.Equal(AttendanceErrors.QrModeNotEnabled(), result.Error);
                Assert.Null(store.Call);
            }
        }

        [Fact]
        public async Task IssueHandler_ForwardsTheBranchAndTheActorInQrMode()
        {
            var store = new FakeQrStore();
            var handler = new IssueAttendanceQrCommandHandler(
                store, new FakeSettingsReader { Mode = AttendanceMode.Qr.ToString() }, User());

            var result = await handler.Handle(new IssueAttendanceQrCommand(BranchId), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(Token, result.Value.Token);
            Assert.Equal((BranchId, ActorId), Assert.IsType<(Guid, Guid)>(store.Call));
        }

        [Fact]
        public async Task IssueHandler_ForStaffRefusesAForeignBranchBeforeReadingSettings()
        {
            var store = new FakeQrStore();
            var settingsReader = new FakeSettingsReader { Mode = AttendanceMode.Qr.ToString() };
            var handler = new IssueAttendanceQrCommandHandler(store, settingsReader, StaffUser(OtherBranchId));

            var result = await handler.Handle(new IssueAttendanceQrCommand(BranchId), default);

            Assert.Equal(AttendanceErrors.BranchNotFound(), result.Error);
            Assert.Null(store.Call);
            Assert.Equal(0, settingsReader.Reads);
        }

        [Fact]
        public async Task IssueHandler_AllowsAStaffMemberOfTheSameBranch()
        {
            var store = new FakeQrStore();
            var handler = new IssueAttendanceQrCommandHandler(
                store, new FakeSettingsReader { Mode = AttendanceMode.Qr.ToString() }, StaffUser(BranchId));

            var result = await handler.Handle(new IssueAttendanceQrCommand(BranchId), default);

            Assert.True(result.IsSuccess);
            Assert.NotNull(store.Call);
        }

        [Fact]
        public async Task IssueHandler_RefusesAMissingTenantActor()
        {
            var store = new FakeQrStore();
            var handler = new IssueAttendanceQrCommandHandler(
                store,
                new FakeSettingsReader { Mode = AttendanceMode.Qr.ToString() },
                new FakeTenantUser { UserId = Guid.Empty });

            var result = await handler.Handle(new IssueAttendanceQrCommand(BranchId), default);

            Assert.Equal(AuthErrors.Forbidden(), result.Error);
            Assert.Null(store.Call);
        }

        [Fact]
        public async Task IssueHandler_RefusesAMissingSettingsRow()
        {
            var store = new FakeQrStore();
            var handler = new IssueAttendanceQrCommandHandler(
                store, new FakeSettingsReader { ReturnNull = true }, User());

            var result = await handler.Handle(new IssueAttendanceQrCommand(BranchId), default);

            Assert.Equal(AttendanceErrors.QrModeNotEnabled(), result.Error);
            Assert.Null(store.Call);
        }

        private static FakeTenantUser User() => new();

        private static FakeTenantUser StaffUser(Guid branchId) => new()
        {
            BranchId = branchId,
            AccountAccess = new TenantAccountAccess(true, "1", true) { BranchId = branchId },
        };

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = ActorId;

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Owner" };

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; } = new(false, "1", false);
        }

        private sealed class FakeSettingsReader : IAttendanceSettingsReader
        {
            public string? Mode { get; init; } = AttendanceMode.Qr.ToString();

            public bool ReturnNull { get; init; }

            public int Reads { get; private set; }

            public Task<AttendanceSettingsResponse?> GetAsync(CancellationToken cancellationToken)
            {
                Reads++;
                if (ReturnNull)
                {
                    return Task.FromResult<AttendanceSettingsResponse?>(null);
                }

                return Task.FromResult<AttendanceSettingsResponse?>(
                    new AttendanceSettingsResponse(Mode!, "1"));
            }
        }

        private sealed class FakeQrStore : IAttendanceQrSessionStore
        {
            public (Guid BranchId, Guid ActorUserId)? Call { get; private set; }

            public Task<Result<AttendanceQrSessionResponse>> IssueAsync(
                Guid branchId, Guid actorUserId, CancellationToken cancellationToken)
            {
                Call = (branchId, actorUserId);
                return Task.FromResult(Result.Success(
                    new AttendanceQrSessionResponse(branchId, Token, Now, Now.AddMinutes(10))));
            }
        }
    }
}
