using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc.Controllers;
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
                Description = "Use a JWT access token from admin or tenant sign-in. In Swagger UI, paste the raw token without the 'Bearer ' prefix; Swagger adds that prefix to the Authorization header.",
            };

            return Task.CompletedTask;
        }

        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            if (operation is null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            var actionDescriptor = context?.Description?.ActionDescriptor;
            if (actionDescriptor is null)
            {
                return Task.CompletedTask;
            }

            var metadata = actionDescriptor.EndpointMetadata ?? [];
            var controllerAttributes = actionDescriptor is ControllerActionDescriptor controllerAction
                ? controllerAction.ControllerTypeInfo.GetCustomAttributes(inherit: true)
                    .Concat(controllerAction.MethodInfo.GetCustomAttributes(inherit: true))
                : Enumerable.Empty<object>();
            var authorizationMetadata = metadata.Concat(controllerAttributes).ToArray();

            if (authorizationMetadata.OfType<IAllowAnonymous>().Any()
                || !authorizationMetadata.OfType<IAuthorizeData>().Any())
            {
                return Task.CompletedTask;
            }

            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeName, context!.Document, null)] = new List<string>(),
                },
            ];

            return Task.CompletedTask;
        }
    }
}
