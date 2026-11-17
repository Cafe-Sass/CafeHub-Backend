using CafeHub.Application.Abstractions.Results;
using FluentValidation;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetProductQueryValidator : AbstractValidator<GetProductQuery>
    {
        public GetProductQueryValidator()
        {
            RuleFor(query => query.Id).NotEmpty()
                .WithMessage("The product id is required.")
                .WithErrorCode("Products.InvalidId");
        }
    }
}
