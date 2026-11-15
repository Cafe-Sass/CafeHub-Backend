using CafeHub.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Identity
{
    public sealed class PlatformAdminPasswordPolicy<TUser> : IPasswordValidator<TUser> where TUser : class
    {
        public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string? password)
        {
            return Task.FromResult(NewPasswordPolicy.IsValid(password)
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError { Code = "InvalidNewPassword", Description = NewPasswordPolicy.Requirements }));
        }
    }
}
