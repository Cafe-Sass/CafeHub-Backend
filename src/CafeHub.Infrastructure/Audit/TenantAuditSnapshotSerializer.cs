using System.Text;
using System.Text.Json;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;

namespace CafeHub.Infrastructure.Audit
{
    internal static class TenantAuditSnapshotSerializer
    {
        private const int MaximumStateLength = 4096;
        private const string TruncationMarker = "...[truncated]";
        private const string CafeSettingsTarget = "cafe_settings";
        private const string BranchesTarget = "branches";

        private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowLists =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                [CafeSettingsTarget] = new(StringComparer.Ordinal)
                {
                    "CafeName",
                    "PrimaryColor",
                    "SecondaryColor",
                    "AccentColor",
                    "Timezone",
                },
                [BranchesTarget] = new(StringComparer.Ordinal)
                {
                    "Name",
                    "OpeningHoursSummary",
                    "LifecycleStatus",
                },
            };

        private static readonly HashSet<string> DeniedNames = new(StringComparer.Ordinal)
        {
            "password",
            "passwordhash",
            "token",
            "accesstoken",
            "refreshtoken",
            "secret",
            "apikey",
            "credential",
            "connectionstring",
            "email",
            "contactemail",
            "phone",
            "contactphone",
        };

        public static Result<Snapshots> Create(
            string targetType,
            TenantAuditChanges changes,
            int maximumStateLength = MaximumStateLength)
        {
            if (!AllowLists.TryGetValue(targetType, out var allowList))
            {
                return Result.Failure<Snapshots>(Error.Failure(
                    "Audit.UnsupportedSnapshotTarget",
                    "The audit snapshot target type is not supported."));
            }

            var before = new SortedDictionary<string, string?>(StringComparer.Ordinal);
            var after = new SortedDictionary<string, string?>(StringComparer.Ordinal);
            var seenAllowedFields = new HashSet<string>(StringComparer.Ordinal);

            foreach (var field in changes.Fields)
            {
                if (field is null
                    || string.IsNullOrWhiteSpace(field.Name)
                    || IsDeniedName(field.Name)
                    || !allowList.Contains(field.Name))
                {
                    continue;
                }

                if (!seenAllowedFields.Add(field.Name))
                {
                    return Result.Failure<Snapshots>(Error.Failure(
                        "Audit.InvalidSnapshotChanges",
                        "The audit snapshot contains a duplicate field."));
                }

                if (changes.IsCreate)
                {
                    after.Add(field.Name, field.After);
                }
                else if (!string.Equals(field.Before, field.After, StringComparison.Ordinal))
                {
                    before.Add(field.Name, field.Before);
                    after.Add(field.Name, field.After);
                }
            }

            if (after.Count == 0)
            {
                return Result.Success(new Snapshots(null, null));
            }

            string? beforeState = null;
            if (!changes.IsCreate && !TrySerializeBounded(before, out beforeState, maximumStateLength))
            {
                return Result.Failure<Snapshots>(SnapshotTooLarge());
            }

            string? afterState;
            if (!TrySerializeBounded(after, out afterState, maximumStateLength))
            {
                return Result.Failure<Snapshots>(SnapshotTooLarge());
            }

            return Result.Success(new Snapshots(beforeState, afterState));
        }

        private static bool IsDeniedName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            var normalized = new StringBuilder(name.Length);

            foreach (var character in name)
            {
                if (character is '_' or '-' or ' ')
                {
                    continue;
                }

                normalized.Append(char.ToLowerInvariant(character));
            }

            return DeniedNames.Contains(normalized.ToString());
        }

        private static bool TrySerializeBounded(
            SortedDictionary<string, string?> state,
            out string? serialized,
            int maximumStateLength)
        {
            serialized = JsonSerializer.Serialize(state);
            if (serialized.Length <= maximumStateLength)
            {
                return true;
            }

            var truncatableFields = state
                .Where(pair => pair.Value is not null)
                .Select(pair => (
                    pair.Key,
                    Value: pair.Value!,
                    RuneCount: CountRunes(pair.Value!),
                    SerializedValueLength: JsonSerializer.Serialize(pair.Value).Length))
                .Where(field => field.SerializedValueLength > JsonSerializer.Serialize(TruncationMarker).Length)
                .OrderByDescending(field => field.SerializedValueLength)
                .ThenBy(field => field.Key, StringComparer.Ordinal)
                .ToArray();

            if (truncatableFields.Length == 0)
            {
                serialized = null;
                return false;
            }

            foreach (var field in truncatableFields)
            {
                state[field.Key] = TruncationMarker;
                var minimumSerialized = JsonSerializer.Serialize(state);
                if (minimumSerialized.Length <= maximumStateLength)
                {
                    var fittingPrefixRunes = 0;
                    var failingPrefixRunes = field.RuneCount;
                    while (failingPrefixRunes - fittingPrefixRunes > 1)
                    {
                        var candidatePrefixRunes = fittingPrefixRunes + ((failingPrefixRunes - fittingPrefixRunes) / 2);
                        state[field.Key] = PrefixByRunes(field.Value, candidatePrefixRunes) + TruncationMarker;
                        var candidate = JsonSerializer.Serialize(state);
                        if (candidate.Length <= maximumStateLength)
                        {
                            fittingPrefixRunes = candidatePrefixRunes;
                        }
                        else
                        {
                            failingPrefixRunes = candidatePrefixRunes;
                        }
                    }

                    state[field.Key] = PrefixByRunes(field.Value, fittingPrefixRunes) + TruncationMarker;
                }

                serialized = JsonSerializer.Serialize(state);
                if (serialized.Length <= maximumStateLength)
                {
                    return true;
                }
            }

            serialized = JsonSerializer.Serialize(state);
            if (serialized.Length <= maximumStateLength)
            {
                return true;
            }

            serialized = null;
            return false;
        }

        private static string PrefixByRunes(string value, int runeCount)
        {
            if (runeCount == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var appended = 0;
            foreach (var rune in value.EnumerateRunes())
            {
                if (appended == runeCount)
                {
                    break;
                }

                builder.Append(rune.ToString());
                appended++;
            }

            return builder.ToString();
        }

        private static int CountRunes(string value)
        {
            var count = 0;
            foreach (var _ in value.EnumerateRunes())
            {
                count++;
            }

            return count;
        }

        private static Error SnapshotTooLarge() => Error.Failure(
            "Audit.SnapshotTooLarge",
            "The audit snapshot could not be represented within the supported size limit.");

        internal sealed record Snapshots(string? BeforeState, string? AfterState);
    }
}
