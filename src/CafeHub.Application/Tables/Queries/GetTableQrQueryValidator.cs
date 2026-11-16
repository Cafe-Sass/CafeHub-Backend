using FluentValidation;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class GetTableQrQueryValidator : AbstractValidator<GetTableQrQuery>
    {
        public GetTableQrQueryValidator()
        {
            RuleFor(query => query.BranchId).NotEmpty();
            RuleFor(query => query.TableId).NotEmpty();
        }
    }
}
