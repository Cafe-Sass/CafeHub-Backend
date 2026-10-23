using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CafeHub.WebApi.Documentation
{
    /// <summary>
    /// Registers the shared "ProblemDetails" schema component and the "application/problem+json" error
    /// media type in the generated OpenAPI document. Required members mirror the mandatory body produced
    /// by the single ProblemDetails mapper (type, title, status, detail, code, traceId); instance and
    /// correlationId stay optional because a body carries them only when they exist.
    /// </summary>
    public sealed class ProblemDetailsDocumentTransformer : IOpenApiDocumentTransformer
    {
        private const string ProblemDetailsComponentName = "ProblemDetails";
        private const string ProblemDetailsMediaType = "application/problem+json";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var components = document.Components ??= new OpenApiComponents();
            components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
            components.Responses ??= new Dictionary<string, IOpenApiResponse>();

            components.Schemas[ProblemDetailsComponentName] = CreateProblemDetailsSchema();
            components.Responses[ProblemDetailsComponentName] = new OpenApiResponse
            {
                Description = "Problem details body returned when a request fails.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [ProblemDetailsMediaType] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference(ProblemDetailsComponentName, document, string.Empty),
                    },
                },
            };

            return Task.CompletedTask;
        }

        private static OpenApiSchema CreateProblemDetailsSchema()
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "RFC 9457 problem details body for failed requests.",
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                    ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
                    ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String },
                },
                Required = new HashSet<string> { "type", "title", "status", "detail", "code", "traceId" },
            };
        }
    }
}
