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
        private const string TenantUsersTarget = "tenant_users";
        private const string ProductsTarget = "products";
        private const string OrdersTarget = "orders";


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
                [TenantUsersTarget] = new(StringComparer.Ordinal)
                {
                    "BranchId",
                    "FullName",
                    "IsActive",
                    "RoleId",
                    "PersonalPermissionCodes",
                },
                ["tenant_roles"] = new(StringComparer.Ordinal)
                {
                    "Name",
                    "Status",
                    "PermissionCodes",
                },
                ["menu_categories"] = new(StringComparer.Ordinal)
                {
                    "ParentId",
                    "Name",
                    "SortOrder",
                    "Status",
                },
                ["stations"] = new(StringComparer.Ordinal)
                {
                    "Name",
                    "SortOrder",
                    "Status",
                    "BranchId",
                },
                ["branch_tables"] = new(StringComparer.Ordinal)
                {
                    "Number",
                    "Label",
                    "Status",
                    "BranchId",
                    "count",
                    "fromNumber",
                    "toNumber",
                },
                [OrdersTarget] = new(StringComparer.Ordinal)
                {
                    "Status",
                    "RejectionReason",
                    "OrderNumber",
                },
                ["guest_mutes"] = new(StringComparer.Ordinal)
                {
                    "Reason",
                    "ExpiresAt",
                    "LiftedAt",
                    "BranchId",
                },
                ["payments"] = new(StringComparer.Ordinal)
                {
                    "Method",
                    "Status",
                    "Amount",
                    "BillId",
                    "OrderId",
                },
                [ProductsTarget] = new(StringComparer.Ordinal)
                {
                    "Name",
                    "Kind",
                    "Caffeine",
                    "PreparationMinutes",
                    "Description",
                    "Ingredients",
                    "SortOrder",
                    "Status",
                    "CategoryId",
                    "BranchId",
                    "AssignmentId",
                    "StationId",
                    "IsAvailable",
                    "ReasonCode",
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

            var hasFields = false;
            foreach (var field in changes.Fields)
            {
                if (field is null
                    || string.IsNullOrWhiteSpace(field.Name)
                    || IsDeniedName(field.Name)
                    || !IsAllowedField(targetType, allowList, field.Name))
                {
                    continue;
                }

                if (!seenAllowedFields.Add(field.Name))
                {
                    return Result.Failure<Snapshots>(Error.Failure(
                        "Audit.InvalidSnapshotChanges",
                        "The audit snapshot contains a duplicate field."));
                }

                if (changes.IsCreate || field.HasChanged)
                {
                    hasFields = true;
                    if (!changes.IsCreate && field.BeforePresent)
                    {
                        before.Add(field.Name, field.Before);
                    }

                    if (field.AfterPresent)
                    {
                        after.Add(field.Name, field.After);
                    }
                }
            }

            if (!hasFields)
            {
                return Result.Success(new Snapshots(null, null));
            }

            if (targetType == ProductsTarget)
            {
                var exactBefore = changes.IsCreate ? null : JsonSerializer.Serialize(before);
                var exactAfter = JsonSerializer.Serialize(after);
                return exactBefore?.Length > maximumStateLength || exactAfter.Length > maximumStateLength
                    ? Result.Failure<Snapshots>(SnapshotTooLarge())
                    : Result.Success(new Snapshots(exactBefore, exactAfter));
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

        internal static bool IsAllowedProductField(string name) =>
            !IsDeniedName(name) && IsAllowedField(ProductsTarget, AllowLists[ProductsTarget], name);

        private static bool IsAllowedField(string targetType, HashSet<string> allowList, string name)
        {
            if (allowList.Contains(name))
            {
                return true;
            }

            if (targetType != ProductsTarget)
            {
                return false;
            }

            var parts = name.Split('.');
            return parts.Length == 3 && (
                (parts[0] == "price" && parts[1] is "size" or "addon" && Guid.TryParseExact(parts[2], "D", out _))
                || (Guid.TryParseExact(parts[1], "D", out _) && (
                    (parts[0] == "size" && parts[2] is "name" or "status" or "default")
                    || (parts[0] == "addon" && parts[2] is "name" or "status")
                    || (parts[0] == "tag" && parts[2] is "label" or "status"))));
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
