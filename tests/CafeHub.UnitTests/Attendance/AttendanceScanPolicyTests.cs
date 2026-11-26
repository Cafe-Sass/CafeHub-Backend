using CafeHub.Application.Attendance;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class AttendanceScanPolicyTests
    {
        private static readonly Guid StaffId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        private static readonly string Token = new('a', AttendanceQrSession.TokenLength);
        private static readonly string OtherToken = new('b', AttendanceQrSession.TokenLength);

        [Fact]
        public void Scan_WithNoOpenRecord_DecidesACheckInOnTheSessionBranch()
        {
            var session = Session(BranchId, Token, Now);

            var result = AttendanceScanPolicy.Decide(Context(session: session));

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.False(result.Value.IsCheckOut);
            Assert.Equal(BranchId, result.Value.BranchId);
            Assert.Equal(session.Id, result.Value.SessionId);
        }

        [Fact]
        public void Scan_WithAnOpenRecord_DecidesThePairingCheckOut()
        {
            var checkInSession = Session(BranchId, Token, Now.AddMinutes(-30));
            var record = Record(checkInSession, Now.AddMinutes(-30));
            var checkOutSession = Session(BranchId, OtherToken, Now);

            var result = AttendanceScanPolicy.Decide(
                Context(session: checkOutSession, openRecord: record));

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.True(result.Value.IsCheckOut);
            Assert.Equal(record.Id, result.Value.CheckOutRecordId);
            Assert.Equal(BranchId, result.Value.BranchId);
        }

        [Fact]
        public void Scan_RefusesTheSameTokenTwiceInEitherDirection()
        {
            var checkInSession = Session(BranchId, Token, Now.AddMinutes(-5));
            var record = Record(checkInSession, Now.AddMinutes(-5));

            var replayOfCheckIn = AttendanceScanPolicy.Decide(
                Context(session: checkInSession, openRecord: record, sessionAlreadyUsed: true));
            var replayOfOpenRecordToken = AttendanceScanPolicy.Decide(
                Context(session: checkInSession, openRecord: record));

            Assert.Equal(AttendanceErrors.InvalidToken(), replayOfCheckIn.Error);
            Assert.Equal(AttendanceErrors.InvalidToken(), replayOfOpenRecordToken.Error);
        }

        [Fact]
        public void Scan_RefusesAnUnknownExpiredOrForeignBranchTokenUniformly()
        {
            var expired = AttendanceScanPolicy.Decide(
                Context(session: Session(BranchId, Token, Now.AddMinutes(-11))));
            var unknown = AttendanceScanPolicy.Decide(Context(session: null));
            var foreign = AttendanceScanPolicy.Decide(
                Context(session: Session(OtherBranchId, Token, Now)));

            Assert.Equal(AttendanceErrors.InvalidToken(), expired.Error);
            Assert.Equal(AttendanceErrors.InvalidToken(), unknown.Error);
            Assert.Equal(AttendanceErrors.InvalidToken(), foreign.Error);
            Assert.Equal("Attendance.InvalidToken", expired.Error!.Code);
        }

        [Fact]
        public void Scan_IsRefusedOutsideQrMode()
        {
            var session = Session(BranchId, Token, Now);

            foreach (var mode in new[] { AttendanceMode.Off, AttendanceMode.Device })
            {
                var result = AttendanceScanPolicy.Decide(Context(session: session, mode: mode));

                Assert.Equal(AttendanceErrors.InvalidToken(), result.Error);
            }
        }

        [Fact]
        public void Scan_RefusesADeactivatedOrUnassignedActor()
        {
            var session = Session(BranchId, Token, Now);

            var inactive = AttendanceScanPolicy.Decide(Context(session: session, actorIsActive: false));
            var noBranch = AttendanceScanPolicy.Decide(Context(session: session, withoutBranch: true));
            var emptyActor = AttendanceScanPolicy.Decide(Context(session: session, actorUserId: Guid.Empty));

            Assert.Equal(AuthErrors.Forbidden(), inactive.Error);
            Assert.Equal(AuthErrors.Forbidden(), noBranch.Error);
            Assert.Equal(AuthErrors.Forbidden(), emptyActor.Error);
        }

        [Fact]
        public void Record_ClosesOnceWithADifferentSessionOnly()
        {
            var checkInSession = Session(BranchId, Token, Now.AddMinutes(-30));
            var record = Record(checkInSession, Now.AddMinutes(-30));
            var sessionId = Session(BranchId, OtherToken, Now).Id;

            Assert.True(record.IsOpen);
            Assert.False(record.CanCheckOutWith(checkInSession.Id));
            Assert.True(record.CanCheckOutWith(sessionId));

            record.CheckOut(sessionId, Now);

            Assert.False(record.IsOpen);
            Assert.Equal(Now, record.CheckOutAt);
            Assert.Equal(sessionId, record.CheckOutSessionId);
            Assert.False(record.CanCheckOutWith(Guid.NewGuid()));
            Assert.Throws<InvalidOperationException>(() => record.CheckOut(Guid.NewGuid(), Now));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Record(checkInSession, Now).CheckOut(sessionId, Now));
        }

        private static AttendanceScanContext Context(
            AttendanceQrSession? session,
            AttendanceRecord? openRecord = null,
            bool sessionAlreadyUsed = false,
            AttendanceMode mode = AttendanceMode.Qr,
            bool actorIsActive = true,
            bool withoutBranch = false,
            Guid? actorBranchId = null,
            Guid? actorUserId = null) =>
            new(
                mode,
                session,
                actorUserId ?? StaffId,
                actorIsActive,
                withoutBranch ? null : actorBranchId ?? BranchId,
                openRecord,
                sessionAlreadyUsed,
                Now);

        private static AttendanceQrSession Session(
            Guid branchId, string token, DateTimeOffset issuedAt) =>
            AttendanceQrSession.Create(Guid.CreateVersion7(), branchId, token, issuedAt, StaffId);

        private static AttendanceRecord Record(AttendanceQrSession session, DateTimeOffset checkInAt) =>
            AttendanceRecord.CheckIn(
                Guid.CreateVersion7(),
                session.BranchId,
                StaffId,
                session.Id,
                AttendanceSource.Qr,
                checkInAt);
    }
}
