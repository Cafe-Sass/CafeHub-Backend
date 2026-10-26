using CafeHub.WebApi.Documentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace CafeHub.UnitTests
{
    public sealed class BearerSecurityTransformerTests
    {
        [Fact]
        public async Task TransformAsync_RegistersTheSharedBearerSchemeComponent()
        {
            var document = new OpenApiDocument();

            await TransformDocumentAsync(document);

            var scheme = Assert.IsType<OpenApiSecurityScheme>(Assert.Single(document.Components!.SecuritySchemes).Value);
            Assert.Equal(SecuritySchemeType.Http, scheme.Type);
            Assert.Equal("bearer", scheme.Scheme);
            Assert.Equal("JWT", scheme.BearerFormat);
        }

        [Fact]
        public async Task OperationTransformer_AddsOneBearerRequirementToAuthorizedOperations()
        {
            var document = new OpenApiDocument();
            var operation = new OpenApiOperation();

            await TransformOperationAsync(document, operation, new AuthorizeAttribute());

            var requirement = Assert.Single(operation.Security!);
            var reference = Assert.Single(requirement.Keys);
            Assert.Equal("Bearer", reference.Reference!.Id);
            Assert.Empty(requirement[reference]);
        }

        [Fact]
        public async Task OperationTransformer_KeepsAnonymousOperationsWithoutSecurityRequirement()
        {
            var document = new OpenApiDocument();
            var operation = new OpenApiOperation();

            await TransformOperationAsync(document, operation, new AuthorizeAttribute(), new AllowAnonymousAttribute());

            Assert.Null(operation.Security);
        }

        [Fact]
        public async Task OperationTransformer_KeepsOperationsWithoutAuthorizationMetadataUnsecured()
        {
            var document = new OpenApiDocument();
            var operation = new OpenApiOperation();

            await TransformOperationAsync(document, operation, Array.Empty<object>());

            Assert.Null(operation.Security);
        }

        [Fact]
        public async Task OperationTransformer_KeepsOperationsWithOnlyAnonymousMetadataUnsecured()
        {
            var document = new OpenApiDocument();
            var operation = new OpenApiOperation();

            await TransformOperationAsync(document, operation, new AllowAnonymousAttribute());

            Assert.Null(operation.Security);
        }

        private static Task TransformDocumentAsync(OpenApiDocument document)
        {
            return new BearerSecurityTransformer()
                .TransformAsync(document, new OpenApiDocumentTransformerContext
                {
                    DocumentName = "v1",
                    DescriptionGroups = [],
                    ApplicationServices = new ServiceCollection().BuildServiceProvider(),
                }, CancellationToken.None);
        }

        private static Task TransformOperationAsync(OpenApiDocument document, OpenApiOperation operation, params object[] endpointMetadata)
        {
            return new BearerSecurityTransformer()
                .TransformAsync(operation, new OpenApiOperationTransformerContext
                {
                    DocumentName = "v1",
                    Document = document,
                    ApplicationServices = new ServiceCollection().BuildServiceProvider(),
                    Description = new ApiDescription
                    {
                        ActionDescriptor = new ActionDescriptor
                        {
                            EndpointMetadata = endpointMetadata,
                        },
                    },
                }, CancellationToken.None);
        }
    }
}
