using FluentValidation;

namespace CafeHub.Application.Orders.Queries
{
    public sealed class GetOrderQueryValidator : AbstractValidator<GetOrderQuery>
    {
        public GetOrderQueryValidator()
        {
            RuleFor(query => query.OrderId)
                .NotEmpty()
                .WithMessage("The order id is required.");
        }
    }
}
