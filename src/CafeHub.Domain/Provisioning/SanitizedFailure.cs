using System.Text.RegularExpressions;

namespace CafeHub.Domain.Provisioning
{
    public sealed class SanitizedFailure
    {
        private const int MaxLength = 128;

        private static readonly Regex ConnectionStringMarker = new(
            @"(Password=|Host=|postgres://)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string SanitizedMessage = "Provisioning failed.";

        private SanitizedFailure(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static SanitizedFailure Create(string? failureDetail)
        {
            if (string.IsNullOrWhiteSpace(failureDetail))
            {
                return new SanitizedFailure(string.Empty);
            }

            if (ConnectionStringMarker.IsMatch(failureDetail))
            {
                return new SanitizedFailure(SanitizedMessage);
            }

            if (failureDetail.Length > MaxLength)
            {
                return new SanitizedFailure(failureDetail[..MaxLength]);
            }

            return new SanitizedFailure(failureDetail);
        }
    }
}
