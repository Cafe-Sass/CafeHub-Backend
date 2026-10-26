namespace CafeHub.Application.Onboarding.DTOs
{
    /// <summary>
    /// The response returned when an onboarding request is approved.
    /// </summary>
    public sealed record ApprovalResponse(Guid RequestId, Guid TenantId, string State);
}
