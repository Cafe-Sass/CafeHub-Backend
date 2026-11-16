using FluentValidation;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class GetTableQueryValidator : AbstractValidator<GetTableQuery>
    {
        public GetTableQueryValidator()
        {
            RuleFor(query => query.TableId)
                .NotEmpty();
        }
    }
}
