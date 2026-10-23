namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// The single place for paging limits. Documented defaults: <see cref="DefaultPageSize"/> 20, <see cref="MaxPageSize"/> 100
    /// (proposed, pending owner confirmation). <see cref="AddApplication"/> binds the values from the
    /// <see cref="PaginationConfigurationKeys.SectionName"/> configuration section through <see cref="PaginationOptionsBinder"/>;
    /// invalid values (default above max, zero, or negative) fail the options validation at startup.
    /// </summary>
    public sealed class PaginationOptions
    {
        public int DefaultPageSize { get; set; } = 20;

        public int MaxPageSize { get; set; } = 100;

        /// <summary>
        /// Applies the documented default page size when the request does not carry <c>pageSize</c>
        /// and fails loudly when a validated request exceeds the configured bounds.
        /// </summary>
        public int ResolvePageSize(int? requestedPageSize)
        {
            if (requestedPageSize is null)
            {
                return DefaultPageSize;
            }

            if (requestedPageSize < 1 || requestedPageSize > MaxPageSize)
            {
                throw new InvalidOperationException(
                    $"{PaginationConfigurationKeys.DefaultPageSizeKey} must be validated between 1 and {MaxPageSize} before the page size is resolved.");
            }

            return requestedPageSize.Value;
        }

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (DefaultPageSize < 1)
            {
                problems.Add($"{PaginationConfigurationKeys.DefaultPageSizePath} must be at least 1 but was {DefaultPageSize}.");
            }

            if (MaxPageSize < DefaultPageSize)
            {
                problems.Add(
                    $"{PaginationConfigurationKeys.MaxPageSizePath} must be at least the {PaginationConfigurationKeys.DefaultPageSizePath} of {DefaultPageSize} but was {MaxPageSize}.");
            }

            return problems;
        }
    }
}
