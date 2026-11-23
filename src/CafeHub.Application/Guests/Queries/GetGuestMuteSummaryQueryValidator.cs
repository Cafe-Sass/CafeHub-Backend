using FluentValidation;

namespace CafeHub.Application.Guests.Queries
{
    public sealed class GetGuestMuteSummaryQueryValidator : AbstractValidator<GetGuestMuteSummaryQuery>
    {
        public GetGuestMuteSummaryQueryValidator()
        {
            RuleFor(query => query)
                .Custom((query, context) =>
                {
                    if (query.From > query.To)
                    {
                        context.AddFailure(GuestMuteErrors.DateRangeInvalid().Message);
                    }
                });
        }
    }
}
