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

            var components = document.Components;
            Assert.NotNull(components);
            var schemas = components.Schemas;
            Assert.NotNull(schemas);
            var schema = Assert.Contains("ProblemDetails", schemas) as OpenApiSchema;

            Assert.NotNull(schema);
            Assert.Equal(JsonSchemaType.Object, schema.Type);
        }

        [Fact]
        public async Task TransformAsync_DocumentsProblemDetailsMembers()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var components = document.Components;
            Assert.NotNull(components);
            var schemas = components.Schemas;
            Assert.NotNull(schemas);
            var schema = (OpenApiSchema)schemas["ProblemDetails"];
            Assert.NotNull(schema);
            var properties = schema.Properties;
            Assert.NotNull(properties);

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

            var components = document.Components;
            Assert.NotNull(components);
            var responses = components.Responses;
            Assert.NotNull(responses);
            var response = (OpenApiResponse)responses["ProblemDetails"];
            Assert.NotNull(response);
            var content = response.Content;
            Assert.NotNull(content);
            var mediaType = content["application/problem+json"];
            Assert.NotNull(mediaType);

            var schemaReference = Assert.IsType<OpenApiSchemaReference>(mediaType.Schema);
            Assert.Equal("ProblemDetails", schemaReference.Reference.Id);
        }

        [Fact]
        public async Task TransformAsync_MarksMandatoryMapperMembersAsRequired()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var components = document.Components;
            Assert.NotNull(components);
            var schemas = components.Schemas;
            Assert.NotNull(schemas);
            var schema = (OpenApiSchema)schemas["ProblemDetails"];
            Assert.NotNull(schema);

            Assert.Equal(
                new HashSet<string> { "type", "title", "status", "detail", "code", "traceId" },
                schema.Required);
        }

        [Fact]
        public async Task TransformAsync_KeepsCorrelationIdOptionalForBodiesWithoutCorrelation()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var components = document.Components;
            Assert.NotNull(components);
            var schemas = components.Schemas;
            Assert.NotNull(schemas);
            var schema = (OpenApiSchema)schemas["ProblemDetails"];
            Assert.NotNull(schema);
            Assert.NotNull(schema.Properties);
            Assert.NotNull(schema.Required);

            Assert.True(schema.Properties.ContainsKey("correlationId"));
            Assert.False(schema.Required.Contains("correlationId"));
        }

        [Fact]
        public async Task TransformAsync_RecordsStatusAsAnInt32AndInstanceAsAUri()
        {
            var document = new OpenApiDocument();

            await TransformAsync(document);

            var components = document.Components;
            Assert.NotNull(components);
            var schemas = components.Schemas;
            Assert.NotNull(schemas);
            var schema = (OpenApiSchema)schemas["ProblemDetails"];
            Assert.NotNull(schema);
            Assert.NotNull(schema.Properties);
            var statusSchema = schema.Properties["status"];
            Assert.NotNull(statusSchema);
            var instanceSchema = schema.Properties["instance"];
            Assert.NotNull(instanceSchema);

            Assert.Equal(JsonSchemaType.Integer, statusSchema.Type);
            Assert.Equal("int32", statusSchema.Format);
            Assert.Equal("uri", instanceSchema.Format);
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
