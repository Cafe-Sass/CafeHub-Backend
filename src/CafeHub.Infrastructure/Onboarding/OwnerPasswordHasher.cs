using CafeHub.Application.Onboarding;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Onboarding
{
    internal sealed class OwnerPasswordHasher : IOwnerPasswordHasher
    {
        private readonly PasswordHasher<object> hasher = new();

        public string Hash(string password)
        {
            return hasher.HashPassword(null, password);
        }
    }
}
