using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace CafeHub.WebApi.Documentation
{
    /// <summary>
    /// Decides whether the OpenAPI document endpoint is exposed. The Development environment always exposes
    /// the document; every other environment does not unless the <c>OpenApi:ExposeOutsideDevelopment</c>
    /// configuration flag is true. A missing or unparsable flag value behaves as false.
    /// </summary>
    public sealed class OpenApiExposurePolicy
    {
        public const string ExposeOutsideDevelopmentKey = "OpenApi:ExposeOutsideDevelopment";

        private readonly IHostEnvironment environment;
        private readonly bool exposeOutsideDevelopment;

        public OpenApiExposurePolicy(IHostEnvironment environment, IConfiguration configuration)
        {
            if (environment is null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            this.environment = environment;
            this.exposeOutsideDevelopment = bool.TryParse(configuration[ExposeOutsideDevelopmentKey], out var exposeOutsideDevelopment) && exposeOutsideDevelopment;
        }

        public bool IsExposed()
        {
            return this.environment.IsDevelopment() || this.exposeOutsideDevelopment;
        }
    }
}
