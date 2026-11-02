using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace CafeHub.WebApi.Transport
{
    /// <summary>
    /// Decides whether HTTPS enforcement applies. Development is the only exempt environment; every other
    /// environment, including an unknown name, fails closed and enforces HTTPS.
    /// </summary>
    public static class TransportSecurityPolicy
    {
        public static bool ShouldApplyHttps(IHostEnvironment environment)
        {
            if (environment is null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            return !environment.IsDevelopment();
        }
    }
}
