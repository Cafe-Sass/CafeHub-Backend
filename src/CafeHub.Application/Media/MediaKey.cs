using System.Globalization;

namespace CafeHub.Application.Media
{
    public sealed record MediaKey
    {
        private const int IdLength = 32;
        private const string PlatformSegment = "platform";
        private const string TenantsSegment = "tenants";

        private MediaKey(MediaScope scope, string id, string value)
        {
            Scope = scope;
            Id = id;
            Value = value;
        }

        public MediaScope Scope { get; }

        public string Id { get; }

        public string Value { get; }

        public static MediaKey Create(MediaScope scope)
        {
            var id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            return new MediaKey(scope, id, BuildValue(scope, id));
        }

        public static bool TryParse(string? value, out MediaKey? key)
        {
            key = null;

            if (string.IsNullOrWhiteSpace(value)
                || value.Contains("..", StringComparison.Ordinal)
                || value.Contains('\\')
                || value.Contains('\0'))
            {
                return false;
            }

            var segments = value.Split('/');
            if (segments.Length == 2 && string.Equals(segments[0], PlatformSegment, StringComparison.Ordinal))
            {
                return TryCreate(MediaScope.Platform, segments[1], value, out key);
            }

            if (segments.Length == 3 && string.Equals(segments[0], TenantsSegment, StringComparison.Ordinal))
            {
                if (!Guid.TryParseExact(segments[1], "D", out var tenantId)
                    || tenantId == Guid.Empty
                    || !string.Equals(segments[1], tenantId.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    return false;
                }

                return TryCreate(MediaScope.ForTenant(tenantId), segments[2], value, out key);
            }

            return false;
        }

        private static string BuildValue(MediaScope scope, string id)
        {
            return scope.TenantId is { } tenantId
                ? string.Create(CultureInfo.InvariantCulture, $"{TenantsSegment}/{tenantId:D}/{id}")
                : $"{PlatformSegment}/{id}";
        }

        private static bool TryCreate(MediaScope scope, string id, string value, out MediaKey? key)
        {
            key = null;

            if (id.Length != IdLength || !IsLowerHex(id))
            {
                return false;
            }

            key = new MediaKey(scope, id, value);

            return true;
        }

        private static bool IsLowerHex(string value)
        {
            foreach (var character in value)
            {
                var isLowerHex = character is (>= '0' and <= '9') or (>= 'a' and <= 'f');
                if (!isLowerHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
