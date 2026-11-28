using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Dashboard
{
    public static class DashboardErrors
    {
        public const string InvalidRangeCode = "Dashboard.InvalidRange";
        public const string TimeZoneUnavailableCode = "Dashboard.TimeZoneUnavailable";
        public const string UnknownMetricCode = "Dashboard.UnknownMetric";

        public static Error InvalidRange(string message) =>
            Error.Validation(InvalidRangeCode, message);

        public static Error UnknownMetric(string? metric) =>
            Error.NotFound(
                UnknownMetricCode,
                $"The dashboard metric '{metric}' is not in the metric catalog.");

        public static Error TimeZoneUnavailable() =>
            Error.Failure(
                TimeZoneUnavailableCode,
                "The cafe time zone is missing or is not recognized, so the report range cannot be resolved.");
    }
}
