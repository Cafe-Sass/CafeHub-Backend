namespace CafeHub.Application.Refunds.DTOs
{
    public sealed record AssignRefundRequestResult(
        Guid RefundRequestId,
        Guid AssignedUserId,
        string Status);
}
