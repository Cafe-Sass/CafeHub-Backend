using CafeHub.Domain.Tenants;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Wifi.Commands
{
    internal static class WifiPackageValidation
    {
        internal static void ValidateQuotaMode<T>(
            WifiDataMode dataMode,
            int? quotaMb,
            ValidationContext<T> context)
        {
            if (dataMode == WifiDataMode.Limited && quotaMb is null)
            {
                context.AddFailure(new ValidationFailure(
                    "QuotaMb", "A limited data mode requires a quota in MB."));
            }

            if (dataMode == WifiDataMode.Unlimited && quotaMb is not null)
            {
                context.AddFailure(new ValidationFailure(
                    "QuotaMb", "An unlimited data mode cannot carry a quota."));
            }
        }
    }
}
