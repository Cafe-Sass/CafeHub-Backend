namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// The anchor of a keyset page: the verified sort fingerprint, the anchor row's sort values as strings in
    /// fingerprint order (the anchor's own sort columns, excluding the identifier tiebreaker that travels as
    /// <see cref="LastId"/>), and the anchor row's identifier.
    /// </summary>
    public sealed record CursorPage
    {
        public CursorPage(SortFingerprint fingerprint, IReadOnlyList<string> sortValues, Guid lastId)
        {
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            SortValues = sortValues ?? throw new ArgumentNullException(nameof(sortValues));

            if (lastId == Guid.Empty)
            {
                throw new ArgumentException("The cursor anchor identifier must not be empty.", nameof(lastId));
            }

            LastId = lastId;
        }

        public SortFingerprint Fingerprint { get; }

        public IReadOnlyList<string> SortValues { get; }

        public Guid LastId { get; }
    }
}
