using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Onboarding
{
    public static class OnboardingErrors
    {
        public const string SlugInvalidCode = "Onboarding.SlugInvalid";
        public const string SlugInvalidMessage = "The slug is not valid.";

        public const string SlugTakenCode = "Onboarding.SlugTaken";
        public const string SlugTakenMessage = "The slug is already taken.";

        public static Error SlugInvalid()
        {
            return Error.Validation(SlugInvalidCode, SlugInvalidMessage);
        }

        public static Error SlugTaken()
        {
            return Error.Conflict(SlugTakenCode, SlugTakenMessage);
        }

        public const string PlanVersionNotFoundCode = "Onboarding.PlanVersionNotFound";
        public const string PlanVersionNotFoundMessage = "The plan version was not found.";

        public static Error PlanVersionNotFound()
        {
            return Error.NotFound(PlanVersionNotFoundCode, PlanVersionNotFoundMessage);
        }

        public const string RequestNotFoundCode = "Onboarding.RequestNotFound";
        public const string RequestNotFoundMessage = "The onboarding request was not found.";

        public static Error RequestNotFound()
        {
            return Error.NotFound(RequestNotFoundCode, RequestNotFoundMessage);
        }

        public const string RequestNotPayableCode = "Onboarding.RequestNotPayable";
        public const string RequestNotPayableMessage = "The onboarding request is not payable.";

        public static Error RequestNotPayable()
        {
            return Error.Conflict(RequestNotPayableCode, RequestNotPayableMessage);
        }
    }
}
