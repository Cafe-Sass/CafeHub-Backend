using CafeHub.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CafeHub.UnitTests
{
    public sealed class ApiDocumentationMappingTests
    {
        [Fact]
        public async Task MapApiDocumentation_MapsRootRedirectToSwaggerWhenDocumentationIsExposed()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development",
            });
            builder.Services.AddApiDocumentation();
            await using var app = builder.Build();
            app.MapApiDocumentation();

            var rootEndpoint = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Single(endpoint => endpoint.RoutePattern.RawText == "/");
            var context = new DefaultHttpContext
            {
                RequestServices = app.Services,
            };

            await rootEndpoint.RequestDelegate!(context);

            Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
            Assert.Equal("/swagger", context.Response.Headers.Location);
        }
    }
}
