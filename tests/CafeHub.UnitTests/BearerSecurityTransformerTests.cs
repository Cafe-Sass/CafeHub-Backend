using System.Reflection;
using System.Text.Json;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.Documentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
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
            Assert.Contains("raw token without the 'Bearer ' prefix", scheme.Description, StringComparison.Ordinal);
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

        [Theory]
        [InlineData(typeof(AdminController), "Me")]
        [InlineData(typeof(TenantMeController), "Me")]
        public async Task OperationTransformer_DiscoversControllerAuthorizationAttributes(
            Type controllerType,
            string actionName)
        {
            var document = new OpenApiDocument();
            var operation = new OpenApiOperation();

            await TransformControllerOperationAsync(document, operation, controllerType, actionName);

            var requirement = Assert.Single(operation.Security!);
            var reference = Assert.Single(requirement.Keys);
            Assert.Equal(BearerSecurityTransformer.SchemeName, reference.Reference!.Id);
        }

        [Fact]
        public async Task OperationTransformer_LeavesAdminLoginPublic()
        {
            var document = new OpenApiDocument();
            var operation = new OpenApiOperation();

            await TransformControllerOperationAsync(document, operation, typeof(AdminAuthController), "Login");

            Assert.Null(operation.Security);
        }

        [Fact]
        public async Task OperationTransformer_SerializesBearerRequirementForProtectedAdminOperation()
        {
            var document = new OpenApiDocument();
            var operation = new OpenApiOperation();
            await TransformDocumentAsync(document);
            await TransformControllerOperationAsync(document, operation, typeof(AdminController), "Me");
            document.Paths = new OpenApiPaths
            {
                ["/api/v1/admin/me"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = operation,
                    },
                },
            };

            using var output = new StringWriter();
            document.SerializeAs(OpenApiSpecVersion.OpenApi3_0, new OpenApiJsonWriter(output));
            using var serialized = JsonDocument.Parse(output.ToString());
            var root = serialized.RootElement;

            Assert.Equal("http", root.GetProperty("components").GetProperty("securitySchemes")
                .GetProperty("Bearer").GetProperty("type").GetString());
            Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes")
                .GetProperty("Bearer").GetProperty("scheme").GetString());
            Assert.Equal("Bearer", root.GetProperty("paths").GetProperty("/api/v1/admin/me")
                .GetProperty("get").GetProperty("security")[0].EnumerateObject().Single().Name);
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

        private static Task TransformControllerOperationAsync(
            OpenApiDocument document,
            OpenApiOperation operation,
            Type controllerType,
            string actionName)
        {
            var method = controllerType.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Could not find {controllerType.Name}.{actionName}.");

            return new BearerSecurityTransformer()
                .TransformAsync(operation, new OpenApiOperationTransformerContext
                {
                    DocumentName = "v1",
                    Document = document,
                    ApplicationServices = new ServiceCollection().BuildServiceProvider(),
                    Description = new ApiDescription
                    {
                        ActionDescriptor = new ControllerActionDescriptor
                        {
                            ControllerTypeInfo = controllerType.GetTypeInfo(),
                            MethodInfo = method,
                            EndpointMetadata = [],
                        },
                    },
                }, CancellationToken.None);
        }
    }
}
