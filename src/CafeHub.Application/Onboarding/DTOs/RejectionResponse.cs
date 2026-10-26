namespace CafeHub.Application.Onboarding.DTOs
{
    /// <summary>
    /// The response returned when an onboarding request is rejected.
    /// </summary>
    public sealed record RejectionResponse(Guid RequestId, string State);
}
