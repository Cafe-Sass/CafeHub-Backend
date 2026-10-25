using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Onboarding
{
    /// <summary>
    /// Error codes and messages for the onboarding module, following the <c>&lt;Module&gt;.&lt;Reason&gt;</c>
    /// convention with module <c>Onboarding</c>. A slug that violates the format or reserved-word rules maps to
    /// <c>Onboarding.SlugInvalid</c> (Validation, field <c>slug</c>); a slug already in use maps to
    /// <c>Onboarding.SlugTaken</c> (Conflict). A duplicate public-registration email is not an error: it is the
    /// <see cref="ExistingRequest"/> outcome.
    /// </summary>
    public static class OnboardingErrors
    {
        public const string SlugInvalidCode = "Onboarding.SlugInvalid";
        public const string SlugInvalidMessage = "The slug is not valid.";

        public const string SlugTakenCode = "Onboarding.SlugTaken";
        public const string SlugTakenMessage = "The slug is already taken.";

        public static ValidationError SlugInvalid()
        {
            return ValidationError.Create(new[] { new FieldError("slug", SlugInvalidMessage) });
        }

        public static Error SlugTaken()
        {
            return Error.Conflict(SlugTakenCode, SlugTakenMessage);
        }
    }
}
