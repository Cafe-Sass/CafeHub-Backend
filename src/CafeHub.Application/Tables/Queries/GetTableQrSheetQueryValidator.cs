using FluentValidation;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class GetTableQrSheetQueryValidator : AbstractValidator<GetTableQrSheetQuery>
    {
        public GetTableQrSheetQueryValidator()
        {
            RuleFor(query => query.BranchId).NotEmpty();
        }
    }
}
