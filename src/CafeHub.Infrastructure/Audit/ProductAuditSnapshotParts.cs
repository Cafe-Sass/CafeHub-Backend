using System.Text.Json;
using CafeHub.Application.Audit;

namespace CafeHub.Infrastructure.Audit
{
    internal static class ProductAuditSnapshotParts
    {
        // Even the largest JSON escape per UTF-16 code unit fits with the envelope.
        private const int FragmentLength = 256;

        public static IReadOnlyList<TenantAuditSnapshotSerializer.Snapshots> Create(TenantAuditChanges changes)
        {
            var fragments = new List<(string Field, int Index, Fragment Before, Fragment After)>();
            foreach (var field in changes.Fields.OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                if (!TenantAuditSnapshotSerializer.IsAllowedProductField(field.Name)
                    || (!changes.IsCreate && !field.HasChanged))
                {
                    continue;
                }

                var before = field.BeforePresent ? Split(field.Before) : [];
                var after = field.AfterPresent ? Split(field.After) : [];
                for (var index = 0; index < Math.Max(before.Count, after.Count); index++)
                {
                    fragments.Add((field.Name, index,
                        new Fragment(field.BeforePresent, before.Count, before.ElementAtOrDefault(index)),
                        new Fragment(field.AfterPresent, after.Count, after.ElementAtOrDefault(index))));
                }
            }

            var id = Guid.CreateVersion7();
            return fragments.Select((part, index) => new TenantAuditSnapshotSerializer.Snapshots(
                changes.IsCreate ? null : Serialize(part.Field, part.Index, part.Before, index),
                Serialize(part.Field, part.Index, part.After, index))).ToArray();

            string Serialize(string field, int fragment, Fragment side, int index) =>
                JsonSerializer.Serialize(new
                {
                    changeSetId = id,
                    part = index + 1,
                    partCount = fragments.Count,
                    field,
                    fragment,
                    present = side.Present,
                    fragmentCount = side.Count,
                    value = side.Value,
                });

        }

        private readonly record struct Fragment(bool Present, int Count, string? Value);

        private static IReadOnlyList<string?> Split(string? value)
        {
            if (value is null || value.Length <= FragmentLength)
            {
                return [value];
            }

            var fragments = new List<string?>();
            var offset = 0;
            while (offset < value.Length)
            {
                var length = Math.Min(FragmentLength, value.Length - offset);
                if (offset + length < value.Length && char.IsHighSurrogate(value[offset + length - 1]))
                {
                    length--;
                }

                fragments.Add(value.Substring(offset, length));
                offset += length;
            }

            return fragments;
        }
    }
}
