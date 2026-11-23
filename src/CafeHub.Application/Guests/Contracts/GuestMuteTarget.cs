namespace CafeHub.Application.Guests.Contracts
{
    public sealed record GuestMuteTarget(Guid OrderId, Guid BranchId, int OrderNumber, string? DeviceId);
}
