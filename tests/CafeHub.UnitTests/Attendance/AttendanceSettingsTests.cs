using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Commands;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class AttendanceSettingsTests
    {
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Settings_DefaultToOffWhenCreated()
        {
            var settings = AttendanceSettings.Create(Guid.CreateVersion7(), Now, ActorId);

            Assert.Equal(AttendanceMode.Off, settings.Mode);
            Assert.False(settings.AllowsQrSessions);
            Assert.Equal(Now, settings.CreatedAt);
            Assert.Equal(Now, settings.UpdatedAt);
            Assert.Throws<ArgumentException>(() => AttendanceSettings.Create(Guid.Empty, Now, ActorId));
        }

        [Fact]
        public void Settings_ChangeOneModeAtATimeAndReportNoChange()
        {
            var settings = AttendanceSettings.Create(Guid.CreateVersion7(), Now, ActorId);

            Assert.True(settings.Update(AttendanceMode.Qr, Now.AddMinutes(1), ActorId));
            Assert.Equal(AttendanceMode.Qr, settings.Mode);
            Assert.True(settings.AllowsQrSessions);
            Assert.Equal(Now.AddMinutes(1), settings.UpdatedAt);
            Assert.False(settings.Update(AttendanceMode.Qr, Now.AddMinutes(2), ActorId));
            Assert.Equal(Now.AddMinutes(1), settings.UpdatedAt);
            Assert.True(settings.Update(AttendanceMode.Device, Now.AddMinutes(3), ActorId));
            Assert.Equal(AttendanceMode.Device, settings.Mode);
            Assert.False(settings.AllowsQrSessions);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.Update((AttendanceMode)99, Now.AddMinutes(4), ActorId));
        }

        [Fact]
        public void SettingsValidator_AcceptsTheThreeModesAndRefusesAnUndefinedOne()
        {
            var validator = new UpdateAttendanceSettingsCommandValidator();

            Assert.True(validator.Validate(
                new UpdateAttendanceSettingsCommand(AttendanceMode.Off, "1")).IsValid);
            Assert.True(validator.Validate(
                new UpdateAttendanceSettingsCommand(AttendanceMode.Qr, "1")).IsValid);
            Assert.True(validator.Validate(
                new UpdateAttendanceSettingsCommand(AttendanceMode.Device, "1")).IsValid);

            var invalid = validator.Validate(
                new UpdateAttendanceSettingsCommand((AttendanceMode)99, "1"));
            Assert.False(invalid.IsValid);
            Assert.Equal("Attendance.InvalidMode", invalid.Errors[0].ErrorCode);
        }

        [Fact]
        public async Task SettingsHandler_ForwardsTheModeTheActorAndTheToken()
        {
            var writer = new FakeSettingsWriter();
            var handler = new UpdateAttendanceSettingsCommandHandler(writer, User());

            var result = await handler.Handle(
                new UpdateAttendanceSettingsCommand(AttendanceMode.Qr, "7"), default);

            Assert.True(result.IsSuccess);
            var call = Assert.IsType<UpdateCall>(writer.Call);
            Assert.Equal(AttendanceMode.Qr, call.Mode);
            Assert.Equal(ActorId, call.ActorUserId);
            Assert.Equal("7", call.ConcurrencyToken);
        }

        [Fact]
        public async Task SettingsHandler_RefusesAMissingActor()
        {
            var writer = new FakeSettingsWriter();
            var handler = new UpdateAttendanceSettingsCommandHandler(
                writer, new FakeTenantUser { UserId = Guid.Empty });

            var result = await handler.Handle(
                new UpdateAttendanceSettingsCommand(AttendanceMode.Qr, "7"), default);

            Assert.Equal(AuthErrors.Forbidden(), result.Error);
            Assert.Null(writer.Call);
        }

        [Fact]
        public void SettingsAudit_HoldsOnlyTheChangedMode()
        {
            var settings = AttendanceSettings.Create(Guid.CreateVersion7(), Now, ActorId);

            var changes = TenantAuditChangeSets.AttendanceSettingsUpdate(
                settings, AttendanceMode.Qr);

            var field = Assert.Single(changes.Fields);
            Assert.Equal(nameof(AttendanceSettings.Mode), field.Name);
            Assert.Equal("Off", field.Before);
            Assert.Equal("Qr", field.After);

            var snapshot = TenantAuditSnapshotSerializer.Create("attendance_settings", changes);

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            Assert.Contains("\"Qr\"", snapshot.Value.AfterState!, StringComparison.Ordinal);
            Assert.DoesNotContain("token", snapshot.Value.AfterState!, StringComparison.OrdinalIgnoreCase);
        }

        private static FakeTenantUser User() => new();

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = ActorId;

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Owner" };

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; } = new(false, "1", false);
        }

        private sealed class FakeSettingsWriter : IAttendanceSettingsWriter
        {
            public UpdateCall? Call { get; private set; }

            public Task<Result<AttendanceSettingsResponse>> UpdateAsync(
                AttendanceMode mode,
                Guid actorUserId,
                string concurrencyToken,
                CancellationToken cancellationToken)
            {
                Call = new UpdateCall(mode, actorUserId, concurrencyToken);
                return Task.FromResult(
                    Result.Success(new AttendanceSettingsResponse(mode.ToString(), "8")));
            }
        }

        private sealed record UpdateCall(AttendanceMode Mode, Guid ActorUserId, string ConcurrencyToken);
    }
}
