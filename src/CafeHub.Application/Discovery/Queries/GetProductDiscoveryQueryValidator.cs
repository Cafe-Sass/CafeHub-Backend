using FluentValidation;

namespace CafeHub.Application.Discovery.Queries
{
    public sealed class GetProductDiscoveryQueryValidator : AbstractValidator<GetProductDiscoveryQuery>
    {
        public GetProductDiscoveryQueryValidator()
        {
            RuleFor(query => query.Query)
                .Must(text => ProductDiscoveryText.Normalize(text).Length > 0)
                .WithMessage("The query is required.")
                .OverridePropertyName("query");

            RuleFor(query => query.Query)
                .Must(text => ProductDiscoveryText.Normalize(text).Length <= ProductDiscoveryText.MaximumLength)
                .WithMessage($"The query must be at most {ProductDiscoveryText.MaximumLength} characters.")
                .OverridePropertyName("query");
        }
    }
}
