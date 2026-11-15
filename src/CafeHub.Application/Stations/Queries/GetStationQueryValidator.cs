using FluentValidation;

namespace CafeHub.Application.Stations.Queries
{
    public sealed class GetStationQueryValidator : AbstractValidator<GetStationQuery>
    {
        public GetStationQueryValidator()
        {
            RuleFor(query => query.StationId)
                .NotEmpty();
        }
    }
}
