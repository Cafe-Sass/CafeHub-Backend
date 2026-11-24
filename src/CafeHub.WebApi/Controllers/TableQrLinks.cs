using System.Globalization;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.WebApi.Controllers
{
    public sealed class TableQrLinks(Uri origin, ITenantContextAccessor tenantContextAccessor)
    {
        public const string ConfigurationKey = "Public:AppBaseUrl";

        public static Uri ParseOrigin(string? value, bool isDevelopment)
        {
            if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || string.IsNullOrEmpty(uri.Host)
                || !IsOriginLiteral(value)
                || (uri.Scheme != Uri.UriSchemeHttps
                    && !(isDevelopment && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            {
                throw new InvalidOperationException(
                    "Public:AppBaseUrl must be an absolute HTTPS origin. HTTP loopback is allowed in Development.");
            }

            return new Uri(uri.GetLeftPart(UriPartial.Authority));
        }

        public TableQrLinkResponse Create(TableQrData table)
        {
            var slug = tenantContextAccessor.GetRequired().Slug
                ?? throw new InvalidTenantContextException("Table QR links require a resolved tenant slug.");
            var path = $"/t/{slug}/q/{table.Code}";
            return new TableQrLinkResponse(
                table.TableId, table.Number, table.Label, path, new Uri(origin, path).AbsoluteUri,
                table.Version.ToString(CultureInfo.InvariantCulture));
        }

        public WifiQrLinkResponse CreateWifi(WifiQrData wifi)
        {
            var slug = tenantContextAccessor.GetRequired().Slug
                ?? throw new InvalidTenantContextException("WiFi QR links require a resolved tenant slug.");
            var path = $"/t/{slug}/w/{wifi.Code}";
            return new WifiQrLinkResponse(wifi.BranchId, path, new Uri(origin, path).AbsoluteUri);
        }

        private static bool IsOriginLiteral(string value)
        {
            var schemeSeparator = value.IndexOf("://", StringComparison.Ordinal);
            if (schemeSeparator < 0 || value.Any(char.IsWhiteSpace))
            {
                return false;
            }

            var authority = value.AsSpan(schemeSeparator + 3);
            if (authority.IndexOfAny('?', '#', '@') >= 0 || authority.Contains('\\'))
            {
                return false;
            }

            var pathStart = authority.IndexOf('/');
            return pathStart < 0 || pathStart == authority.Length - 1;
        }
    }
}
