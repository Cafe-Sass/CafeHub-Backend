namespace CafeHub.Application.Onboarding.Contracts
{
    /// <summary>
    /// Classifies database unique-violation exceptions raised while inserting a public registration. The
    /// permanent public-registration email guard and the slug unique index are distinct constraints, so each
    /// maps to a distinct outcome instead of surfacing as a server error.
    /// </summary>
    public interface IDuplicateRegistrationDetector
    {
        bool IsPublicEmailGuardViolation(Exception exception);
        bool IsSlugTakenViolation(Exception exception);
    }
}
