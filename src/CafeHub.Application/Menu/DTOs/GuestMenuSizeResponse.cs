namespace CafeHub.Application.Menu.DTOs
{
    public sealed record GuestMenuSizeResponse(Guid Id, string Name, decimal Price, bool IsDefault);
}
