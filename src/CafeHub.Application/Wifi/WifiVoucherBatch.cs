using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi
{
    /// <summary>
    /// Normalizes one imported voucher batch: trims every value, keeps the valid first occurrence in
    /// order, counts malformed values as invalid, and counts repeats inside the batch as duplicates.
    /// The comparison is exact and case-sensitive, matching the package-and-code unique index.
    /// </summary>
    public sealed record WifiVoucherBatch(
        IReadOnlyList<string> ValidCodes,
        int DuplicateCodes,
        int InvalidCodes)
    {
        public static WifiVoucherBatch Parse(IEnumerable<string?>? codes)
        {
            ArgumentNullException.ThrowIfNull(codes);

            var valid = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var invalid = 0;
            var duplicate = 0;

            foreach (var raw in codes)
            {
                if (!WifiVoucher.IsValidCode(raw))
                {
                    invalid++;
                    continue;
                }

                if (!seen.Add(raw!.Trim()))
                {
                    duplicate++;
                }
                else
                {
                    valid.Add(raw.Trim());
                }
            }

            return new WifiVoucherBatch(valid, duplicate, invalid);
        }
    }
}
