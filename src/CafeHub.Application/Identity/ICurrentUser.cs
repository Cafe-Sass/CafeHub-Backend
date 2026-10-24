namespace CafeHub.Application.Identity
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }

        Guid? AdminId { get; }

        IReadOnlyCollection<string> Roles { get; }
    }
}
