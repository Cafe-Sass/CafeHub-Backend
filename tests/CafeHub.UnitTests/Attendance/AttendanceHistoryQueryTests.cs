using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Attendance.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;

namespace CafeHub.UnitTests.Attendance
{
    public sealed class AttendanceHistoryQueryTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid StaffId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly DateOnly From = new(2026, 11, 1);
        private static readonly DateOnly To = new(2026, 11, 26);

        [Fact]
        public async Task History_ByTheOwner_RequiresABranch()
        {
            var store = new FakeHistoryStore();
            var handler = Handler(store, Owner());

            var result = await handler.Handle(new GetAttendanceHistoryQuery(), default);

            Assert.Equal(AttendanceErrors.BranchRequired(), result.Error);
            Assert.Null(store.LastRequest);
        }

        [Fact]
        public async Task History_ByTheOwner_UsesTheRequestedBranchAndFilters()
        {
            var store = new FakeHistoryStore();
            var handler = Handler(store, Owner());

            var result = await handler.Handle(
                new GetAttendanceHistoryQuery
                {
                    BranchId = OtherBranchId,
                    StaffId = StaffId,
                    From = From,
                    To = To,
                    Page = 2,
                    PageSize = 5,
                    IncludeTotalCount = true,
                },
                default);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.Page);
            Assert.Equal(5, result.Value.PageSize);
            var request = Assert.IsType<AttendanceHistoryRequest>(store.LastRequest);
            Assert.Equal(OtherBranchId, request.BranchId);
            Assert.Equal(StaffId, request.StaffId);
            Assert.Equal(From, request.From);
            Assert.Equal(To, request.To);
            Assert.Equal(5, request.PageSize);
        }

        [Fact]
        public async Task History_ForStaff_PinsTheirBranchAndRefusesAForeignOne()
        {
            var store = new FakeHistoryStore();
            var handler = Handler(store, Staff());

            var pinned = await handler.Handle(
                new GetAttendanceHistoryQuery { BranchId = BranchId }, default);
            var foreign = await handler.Handle(
                new GetAttendanceHistoryQuery { BranchId = OtherBranchId }, default);

            Assert.True(pinned.IsSuccess);
            Assert.Equal(BranchId, Assert.IsType<AttendanceHistoryRequest>(store.LastRequest).BranchId);
            Assert.Equal(AttendanceErrors.BranchNotFound(), foreign.Error);
        }

        [Fact]
        public void HistoryValidator_RefusesAReversedRangeAndAnEmptyFilter()
        {
            var validator = new GetAttendanceHistoryQueryValidator(new PaginationOptions());

            Assert.True(validator.Validate(
                new GetAttendanceHistoryQuery { From = From, To = To }).IsValid);

            var reversed = validator.Validate(
                new GetAttendanceHistoryQuery { From = To, To = From });
            Assert.False(reversed.IsValid);
            Assert.Contains(reversed.Errors, error => error.ErrorCode == "Attendance.InvalidDateRange");

            var emptyStaff = validator.Validate(
                new GetAttendanceHistoryQuery { StaffId = Guid.Empty });
            Assert.False(emptyStaff.IsValid);
        }

        private static GetAttendanceHistoryQueryHandler Handler(
            IAttendanceHistoryStore store, FakeTenantUser user) =>
            new(store, user, new PaginationOptions());

        private static FakeTenantUser Owner() => new();

        private static FakeTenantUser Staff() => new()
        {
            BranchId = BranchId,
            AccountAccess = new TenantAccountAccess(true, "1", true) { BranchId = BranchId },
        };

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Owner" };

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; } = new(false, "1", false);
        }

        private sealed class FakeHistoryStore : IAttendanceHistoryStore
        {
            public AttendanceHistoryRequest? LastRequest { get; private set; }

            public Task<AttendanceHistoryPage> ListAsync(
                AttendanceHistoryRequest request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new AttendanceHistoryPage(
                    Array.Empty<AttendanceHistoryItem>(), 0));
            }
        }
    }
}
