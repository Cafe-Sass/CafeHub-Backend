namespace CafeHub.Application.Onboarding
{
    public interface IOwnerPasswordHasher
    {
        string Hash(string password);
    }
}
