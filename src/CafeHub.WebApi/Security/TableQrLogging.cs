using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Security
{
    internal static class TableQrLogging
    {
        internal static void ProtectTableQrLogs(this ILoggingBuilder logging)
        {
            logging.Services.PostConfigure<LoggerFilterOptions>(options =>
            {
                // The selected provider rule must enforce the same floor as the category-wide rule.
                for (var i = 0; i < options.Rules.Count; i++)
                {
                    var rule = options.Rules[i];
                    options.Rules[i] = new LoggerFilterRule(
                        rule.ProviderName, rule.CategoryName, rule.LogLevel,
                        (provider, category, level) => Allows(category, level)
                            && (rule.Filter?.Invoke(provider, category, level) ?? true));
                }

                options.Rules.Insert(0, new LoggerFilterRule(
                    null, null, options.MinLevel, (_, category, level) => Allows(category, level)));
            });
        }

        private static bool Allows(string? category, LogLevel level)
        {
            if (StartsWith(category, "Microsoft.AspNetCore.Hosting.Diagnostics")
                || StartsWith(category, "Microsoft.AspNetCore.Server.Kestrel.BadRequests"))
            {
                return false;
            }

            return level >= LogLevel.Warning
                || !(StartsWith(category, "Microsoft.AspNetCore.Mvc")
                    || StartsWith(category, "Microsoft.AspNetCore.Routing")
                    || StartsWith(category, "Microsoft.AspNetCore.HttpsPolicy"));
        }

        private static bool StartsWith(string? category, string prefix) =>
            category?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;
    }
}
