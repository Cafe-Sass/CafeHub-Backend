namespace CafeHub.Infrastructure.Authentication
{
    internal interface ITokenHasher
    {
        string Hash(string rawToken);
    }
}
