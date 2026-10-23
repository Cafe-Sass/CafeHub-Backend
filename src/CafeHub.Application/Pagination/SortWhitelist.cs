using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// The per-list sort whitelist. Each list definition builds one from its allowed field names plus a
    /// default sort (the deterministic-sort rule of <c>architecture/indexing-and-pagination-strategy.md</c>, section 4.1);
    /// the identifier field <c>id</c> must be passed as a requestable field as well, because every resolved sort
    /// ends with it as the tiebreaker (proposed, pending owner confirmation: Create refuses a whitelist without it
    /// instead of silently adding it).
    /// <para>
    /// Resolving a sort request rejects an unknown field with a validation error naming the field, matches field
    /// names case-insensitively, and resolves each name to its canonical form.
    /// </para>
    /// </summary>
    public sealed class SortWhitelist
    {
        /// <summary>
        /// The fixed identifier field used as the deterministic tiebreaker at the end of every resolved sort.
        /// </summary>
        public const string IdentifierField = "id";

        private readonly IReadOnlyDictionary<string, string> canonicalFieldsByNormalizedField;
        private readonly IReadOnlyCollection<string> allowedFields;
        private readonly SortTerm defaultSort;

        private SortWhitelist(IReadOnlyDictionary<string, string> canonicalFieldsByNormalizedField, SortTerm defaultSort)
        {
            this.canonicalFieldsByNormalizedField = canonicalFieldsByNormalizedField;
            this.defaultSort = defaultSort;
            allowedFields = [.. canonicalFieldsByNormalizedField.Values];
        }

        public IReadOnlyCollection<string> AllowedFields => allowedFields;

        public static SortWhitelist Create(IReadOnlyCollection<string> allowedFields, string defaultSortField, SortDirection defaultSortDirection)
        {
            if (allowedFields is null)
            {
                throw new ArgumentNullException(nameof(allowedFields));
            }

            if (allowedFields.Count == 0)
            {
                throw new ArgumentException("A sort whitelist must contain at least one field.", nameof(allowedFields));
            }

            var canonicalFieldsByNormalizedField = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in allowedFields)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    throw new ArgumentException("A sort whitelist field must not be null or whitespace.", nameof(allowedFields));
                }

                if (canonicalFieldsByNormalizedField.ContainsKey(field))
                {
                    throw new ArgumentException($"The sort whitelist contains the duplicate field '{field}'.", nameof(allowedFields));
                }

                canonicalFieldsByNormalizedField[field] = field;
            }

            if (string.IsNullOrWhiteSpace(defaultSortField))
            {
                throw new ArgumentException("The default sort field must not be null or whitespace.", nameof(defaultSortField));
            }

            if (!canonicalFieldsByNormalizedField.TryGetValue(defaultSortField, out var canonicalDefaultField))
            {
                throw new ArgumentException(
                    $"The default sort field '{defaultSortField}' must be one of the whitelisted fields.",
                    nameof(defaultSortField));
            }

            if (!canonicalFieldsByNormalizedField.ContainsKey(IdentifierField))
            {
                throw new ArgumentException(
                    $"A sort whitelist must requestably include the identifier field '{IdentifierField}' so the deterministic tiebreaker never skips or repeats rows.",
                    nameof(allowedFields));
            }

            return new SortWhitelist(
                canonicalFieldsByNormalizedField,
                new SortTerm(canonicalDefaultField, defaultSortDirection));
        }

        public bool Contains(string? field)
        {
            return field is not null && canonicalFieldsByNormalizedField.ContainsKey(field);
        }

        /// <summary>
        /// Resolves a requested sort to an ordered list of <see cref="SortTerm"/>s. An empty or missing sort
        /// resolves to the list default plus the identifier tiebreaker; a non-empty sort keeps the requested term
        /// order and appends the identifier tiebreaker with the direction of the last requested term, unless the
        /// caller already ended with the identifier itself. An unknown field produces the
        /// <see cref="PaginationErrors.SortFieldNotAllowedCode"/> validation error naming the field.
        /// </summary>
        public Result<IReadOnlyList<SortTerm>> Resolve(IReadOnlyList<SortRequest>? requestedSort)
        {
            if (requestedSort is null || requestedSort.Count == 0)
            {
                return Result.Success<IReadOnlyList<SortTerm>>(ResolveDefaultSort());
            }

            var terms = new List<SortTerm>(requestedSort.Count + 1);
            foreach (var request in requestedSort)
            {
                if (request is null || !Contains(request.Field))
                {
                    return Result.Failure<IReadOnlyList<SortTerm>>(PaginationErrors.SortFieldIsNotAllowed(request?.Field));
                }

                terms.Add(new SortTerm(canonicalFieldsByNormalizedField[request.Field!], request.Direction));
            }

            var lastRequestedTerm = terms[^1];
            if (!string.Equals(lastRequestedTerm.Field, IdentifierField, StringComparison.Ordinal))
            {
                terms.Add(new SortTerm(IdentifierField, lastRequestedTerm.Direction));
            }

            return Result.Success<IReadOnlyList<SortTerm>>(terms);
        }

        private IReadOnlyList<SortTerm> ResolveDefaultSort()
        {
            if (string.Equals(defaultSort.Field, IdentifierField, StringComparison.Ordinal))
            {
                return [defaultSort];
            }

            return [defaultSort, new SortTerm(IdentifierField, defaultSort.Direction)];
        }
    }
}
