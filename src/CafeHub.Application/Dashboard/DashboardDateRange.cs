using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Dashboard
{
    public sealed record DashboardRangeInstants(DateTimeOffset From, DateTimeOffset To);

    public static class DashboardDateRange
    {
        public const int MaximumDays = 366;

        public static Result<DashboardRangeInstants> Resolve(
            DateOnly from,
            DateOnly to,
            TimeZoneInfo timeZone)
        {
            ArgumentNullException.ThrowIfNull(timeZone);

            if (to <= from)
            {
                return Result.Failure<DashboardRangeInstants>(
                    DashboardErrors.InvalidRange("The range end must be after the range start."));
            }

            if (to.DayNumber - from.DayNumber > MaximumDays)
            {
                return Result.Failure<DashboardRangeInstants>(
                    DashboardErrors.InvalidRange($"The range cannot exceed {MaximumDays} days."));
            }

            return Result.Success(new DashboardRangeInstants(
                StartOfDay(from, timeZone),
                StartOfDay(to, timeZone)));
        }

        private static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo timeZone)
        {
            var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
        }
    }
}
