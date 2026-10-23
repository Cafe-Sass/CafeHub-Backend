using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// Error codes and messages for the shared Pagination area, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Pagination</c>. Field names are <c>page</c>, <c>pageSize</c>, <c>sort</c>, and <c>cursor</c>.
    /// Messages never echo a rejected cursor.
    /// </summary>
    public static class PaginationErrors
    {
        public const string PageInvalidCode = "Pagination.PageInvalid";
        public const string PageSizeInvalidCode = "Pagination.PageSizeInvalid";
        public const string PageSizeTooLargeCode = "Pagination.PageSizeTooLarge";
        public const string SortFieldNotAllowedCode = "Pagination.SortFieldNotAllowed";
        public const string SortDirectionInvalidCode = "Pagination.SortDirectionInvalid";
        public const string CursorInvalidCode = "Pagination.CursorInvalid";

        public static Error PageIsInvalid()
        {
            return Error.Validation(PageInvalidCode, "The page must start at 1.");
        }

        public static Error PageSizeIsInvalid()
        {
            return Error.Validation(PageSizeInvalidCode, "The page size must be at least 1.");
        }

        public static Error PageSizeIsTooLarge(int maxPageSize)
        {
            return Error.Validation(PageSizeTooLargeCode, $"The page size must not exceed {maxPageSize}.");
        }

        public static Error SortFieldIsNotAllowed(string? requestedField)
        {
            return Error.Validation(
                SortFieldNotAllowedCode,
                string.IsNullOrWhiteSpace(requestedField)
                    ? "A sort field is required."
                    : $"The sort field '{requestedField}' is not allowed for this list.");
        }

        public static Error SortDirectionIsInvalid()
        {
            return Error.Validation(SortDirectionInvalidCode, "The sort direction is not valid.");
        }

        public static Error CursorIsInvalid(string reason)
        {
            return Error.Validation(CursorInvalidCode, $"The cursor is invalid: {reason}");
        }
    }
}
