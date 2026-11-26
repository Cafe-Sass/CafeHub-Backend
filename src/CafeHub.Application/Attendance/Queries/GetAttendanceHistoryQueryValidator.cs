using CafeHub.Application.Pagination;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Attendance.Queries
{
    public sealed class GetAttendanceHistoryQueryValidator : AbstractValidator<GetAttendanceHistoryQuery>
    {
        public GetAttendanceHistoryQueryValidator(PaginationOptions paginationOptions)
        {
            ArgumentNullException.ThrowIfNull(paginationOptions);

            var pageValidator = new OffsetPageRequestValidator(paginationOptions);

            RuleFor(query => query.BranchId)
                .Must(branchId => branchId is null || branchId != Guid.Empty)
                .WithMessage("The branch id cannot be empty.");

            RuleFor(query => query.StaffId)
                .Must(staffId => staffId is null || staffId != Guid.Empty)
                .WithMessage("The staff id cannot be empty.");

            RuleFor(query => query)
                .Custom((query, context) =>
                {
                    if (query.From is { } from && query.To is { } to && from > to)
                    {
                        context.AddFailure(new ValidationFailure("from", AttendanceErrors.InvalidDateRange().Message)
                        {
                            ErrorCode = "Attendance.InvalidDateRange",
                        });
                    }

                    var pageResult = pageValidator.Validate(new OffsetPageRequest
                    {
                        Page = query.Page,
                        PageSize = query.PageSize,
                        IncludeTotalCount = query.IncludeTotalCount,
                    });

                    foreach (var failure in pageResult.Errors)
                    {
                        context.AddFailure(failure);
                    }
                });
        }
    }
}
