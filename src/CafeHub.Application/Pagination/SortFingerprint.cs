using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// A stable value object naming the sorted columns and directions carried inside a keyset cursor payload.
    /// The fingerprint is derived from the resolved <see cref="SortTerm"/> list, so a cursor decoded with a
    /// different sort than the one it was issued for is rejected outright.
    /// </summary>
    public sealed record SortFingerprint
    {
        private SortFingerprint(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static SortFingerprint From(IReadOnlyList<SortTerm> terms)
        {
            if (terms is null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            if (terms.Count == 0)
            {
                throw new InvalidOperationException("The sort fingerprint must carry at least one sort term.");
            }

            return new SortFingerprint(string.Join("|", terms.Select(term => $"{term.Field}:{term.Direction}")));
        }
    }
}
