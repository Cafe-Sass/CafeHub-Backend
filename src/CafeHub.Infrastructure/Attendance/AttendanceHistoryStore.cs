using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Attendance
{
    internal sealed class AttendanceHistoryStore(TenantDbContext context) : IAttendanceHistoryStore
    {
        public async Task<AttendanceHistoryPage> ListAsync(
            AttendanceHistoryRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var records = context.AttendanceRecords.AsNoTracking()
                .Where(record => record.BranchId == request.BranchId);
            if (request.StaffId is { } staffId)
            {
                records = records.Where(record => record.StaffId == staffId);
            }

            if (request.From is { } from)
            {
                var fromAt = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                records = records.Where(record => record.CheckInAt >= fromAt);
            }

            if (request.To is { } to)
            {
                var toAt = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);
                records = records.Where(record => record.CheckInAt < toAt);
            }

            int? totalCount = request.IncludeTotalCount
                ? await records.CountAsync(cancellationToken)
                : null;

            var rows = await records
                .OrderByDescending(record => record.CheckInAt)
                .ThenByDescending(record => record.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(record => new AttendanceHistoryRow(
                    record.Id,
                    record.StaffId,
                    context.TenantUsers
                        .Where(user => user.Id == record.StaffId)
                        .Select(user => user.FullName)
                        .First(),
                    record.BranchId,
                    context.Branches
                        .Where(branch => branch.Id == record.BranchId)
                        .Select(branch => branch.Name)
                        .First(),
                    record.CheckInAt,
                    record.CheckOutAt,
                    record.CheckInSessionId,
                    record.CheckOutSessionId,
                    record.Source))
                .ToListAsync(cancellationToken);

            return new AttendanceHistoryPage(
                rows.Select(row => row.ToItem()).ToArray(), totalCount);
        }

        private sealed record AttendanceHistoryRow(
            Guid Id,
            Guid StaffId,
            string StaffName,
            Guid BranchId,
            string BranchName,
            DateTimeOffset CheckInAt,
            DateTimeOffset? CheckOutAt,
            Guid CheckInSessionId,
            Guid? CheckOutSessionId,
            AttendanceSource Source)
        {
            public AttendanceHistoryItem ToItem() => new(
                Id,
                StaffId,
                StaffName,
                BranchId,
                BranchName,
                CheckInAt,
                CheckOutAt,
                CheckInSessionId,
                CheckOutSessionId,
                Source.ToString());
        }
    }
}
