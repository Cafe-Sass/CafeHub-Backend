using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CafeHub.WebApi.Documentation
{
    /// <summary>
    /// Documents the platform JWT bearer authentication in the generated OpenAPI document: one shared
    /// "Bearer" HTTP security scheme component plus a security requirement on every operation whose
    /// endpoint metadata requires authorization. Endpoints with anonymous metadata, and endpoints without
    /// any authorization metadata (the host defines no fallback policy), stay free of a security
    /// requirement so the document never implies that a public endpoint needs a token.
    /// </summary>
    public sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
    {
        public const string SchemeName = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var components = document.Components ??= new OpenApiComponents();
            components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "A JSON Web Token obtained from the admin sign-in endpoint.",
            };

            return Task.CompletedTask;
        }

        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (context?.Description?.ActionDescriptor?.EndpointMetadata is not { } metadata)
            {
                return Task.CompletedTask;
            }

            if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
            {
                return Task.CompletedTask;
            }

            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, context.Document, string.Empty)] = new List<string>(),
            });

            return Task.CompletedTask;
        }
    }
}
