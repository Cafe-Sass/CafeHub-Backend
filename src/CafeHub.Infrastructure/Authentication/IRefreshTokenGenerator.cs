namespace CafeHub.Infrastructure.Authentication
{
    internal interface IRefreshTokenGenerator
    {
        string Generate();
    }
}
