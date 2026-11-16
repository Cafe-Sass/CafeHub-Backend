namespace CafeHub.Application.Tables.DTOs
{
    public sealed record GuestQrContext(
        string CafeName,
        Guid BranchId,
        string BranchName,
        Guid TableId,
        int TableNumber);
}
