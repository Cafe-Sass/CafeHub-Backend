namespace CafeHub.Application.Onboarding.Contracts
{
    public interface IDuplicateRegistrationDetector
    {
        bool IsPublicEmailGuardViolation(Exception exception);
        bool IsSlugTakenViolation(Exception exception);
        bool IsIdempotencyKeyViolation(Exception exception);
    }
}
