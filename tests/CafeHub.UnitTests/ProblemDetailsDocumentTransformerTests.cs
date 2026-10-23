using CafeHub.WebApi.Documentation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace CafeHub.UnitTests
{
    public sealed class ProblemDetailsDocumentTransformerTests
    {
        [Fact]
        public async Task TransformAsync_AddsSharedProblemDetailsSchemaComponent()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var components = document.Components!;
            var schema = Assert.Contains("ProblemDetails", components.Schemas) as OpenApiSchema;

            Assert.NotNull(schema);
            Assert.Equal(JsonSchemaType.Object, schema!.Type);
        }

        [Fact]
        public async Task TransformAsync_DocumentsProblemDetailsMembers()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var schema = (OpenApiSchema)document.Components!.Schemas["ProblemDetails"];
            var properties = schema.Properties!;

            var expectedKeys = new[] { "type", "title", "status", "detail", "instance", "code", "traceId", "correlationId" };
            foreach (var key in expectedKeys)
            {
                Assert.True(properties.ContainsKey(key), $"Missing schema member '{key}'.");
            }
        }

        [Fact]
        public async Task TransformAsync_GroupsBodiesWithApplicationProblemJsonAsTheErrorMediaType()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var response = (OpenApiResponse)document.Components!.Responses["ProblemDetails"];
            var mediaType = response.Content!["application/problem+json"];

            var schemaReference = Assert.IsType<OpenApiSchemaReference>(mediaType.Schema);
            Assert.Equal("ProblemDetails", schemaReference.Reference!.Id);
        }

        [Fact]
        public async Task TransformAsync_MarksMandatoryMapperMembersAsRequired()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var schema = (OpenApiSchema)document.Components!.Schemas["ProblemDetails"];

            Assert.Equal(
                new HashSet<string> { "type", "title", "status", "detail", "code", "traceId" },
                schema.Required);
        }

        [Fact]
        public async Task TransformAsync_KeepsCorrelationIdOptionalForBodiesWithoutCorrelation()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var schema = (OpenApiSchema)document.Components!.Schemas["ProblemDetails"];

            Assert.True(schema.Properties!.ContainsKey("correlationId"));
            Assert.False(schema.Required!.Contains("correlationId"));
        }

        [Fact]
        public async Task TransformAsync_RecordsStatusAsAnInt32AndInstanceAsAUri()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var schema = (OpenApiSchema)document.Components!.Schemas["ProblemDetails"];
            var status = (OpenApiSchema)schema.Properties!["status"];
            var instance = (OpenApiSchema)schema.Properties!["instance"];

            Assert.Equal(JsonSchemaType.Integer, status.Type);
            Assert.Equal("int32", status.Format);
            Assert.Equal("uri", instance.Format);
        }

        private static Task TransformAsync(OpenApiDocument document)
        {
            var transformer = new ProblemDetailsDocumentTransformer();
            return transformer.TransformAsync(document, CreateContext(), CancellationToken.None);
        }

        private static OpenApiDocumentTransformerContext CreateContext()
        {
            return new OpenApiDocumentTransformerContext
            {
                DocumentName = "v1",
                DescriptionGroups = [],
                ApplicationServices = new ServiceCollection().BuildServiceProvider(),
            };
        }
    }
}
