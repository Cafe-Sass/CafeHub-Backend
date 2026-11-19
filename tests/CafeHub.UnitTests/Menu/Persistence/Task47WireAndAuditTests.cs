using System.Text.Json;
using CafeHub.Application.Audit;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Infrastructure.Audit;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class Task47WireAndAuditTests
    {
        [Fact]
        public async Task AssignmentRoutingStatesMatchSerializedAndGeneratedOpenApiValues()
        {
            string[] approved = ["Assigned", "Unassigned", "StationInactive", "BranchInactive"];
            Assert.Equal(approved, Enum.GetNames<ProductRoutingState>());
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            foreach (var state in Enum.GetValues<ProductRoutingState>())
            {
                var response = new ProductStationAssignmentResponse(Guid.NewGuid(), Guid.NewGuid(), null, state, "token");
                using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, options));
                Assert.Equal(state.ToString(), json.RootElement.GetProperty("routingState").GetString());
            }

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProductRoutingState>("4", options));

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers().AddApplicationPart(typeof(TenantProductStationAssignmentsController).Assembly);
            builder.Services.AddOpenApi();
            await using var application = builder.Build();
            application.MapControllers();
            var generator = application.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
            var document = await generator.GetOpenApiDocumentAsync();
            var responseSchema = document.Components!.Schemas![nameof(ProductStationAssignmentResponse)];
            var routing = responseSchema.Properties!["routingState"];
            Assert.Equal(approved, routing.Enum!.Select(value => value?.GetValue<string>()));
        }

        [Fact]
        public async Task ProductCreateAndListAdvertiseTenantNotFoundInGeneratedOpenApi()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers().AddApplicationPart(typeof(TenantProductsController).Assembly);
            builder.Services.AddOpenApi();
            await using var application = builder.Build();
            application.MapControllers();
            var generator = application.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
            var document = await generator.GetOpenApiDocumentAsync();
            var products = document.Paths["/t/{slug}/api/v1/menu/products"];

            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Get })
            {
                var notFound = products.Operations![method].Responses!["404"];
                Assert.Equal("application/problem+json", Assert.Single(notFound.Content!).Key);
            }
        }

        [Fact]
        public void LegacyWriteShapesAreRejected()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateProductRequest>(
                """{"stationId":null,"kind":"Drink","sortOrder":0,"tags":[]}""", options));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateProductRequest>(
                """{"kind":"Drink","sortOrder":0,"tags":["hot"]}""", options));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SetProductStationAssignmentRequest>(
                """{"concurrencyToken":"token"}""", options));

            var clear = JsonSerializer.Deserialize<SetProductStationAssignmentRequest>(
                """{"stationId":null,"concurrencyToken":"token"}""", options);
            Assert.NotNull(clear);
            Assert.Null(clear.StationId);
        }

        [Fact]
        public void ProductSnapshotsDistinguishMissingNullEmptyAndAbsentFragments()
        {
            var addedTag = $"tag.{Guid.NewGuid():D}.label";
            var shortChanges = TenantAuditChanges.Update(
            [
                new(addedTag, null, "", BeforePresent: false),
                new("Description", null, ""),
            ]);

            var exact = TenantAuditSnapshotSerializer.Create("products", shortChanges).Value;
            using var before = JsonDocument.Parse(exact.BeforeState!);
            using var after = JsonDocument.Parse(exact.AfterState!);
            Assert.False(before.RootElement.TryGetProperty(addedTag, out _));
            Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("Description").ValueKind);
            Assert.Equal("", after.RootElement.GetProperty(addedTag).GetString());

            var oversized = TenantAuditChanges.Update(
                [.. shortChanges.Fields, new("Ingredients", "", new string('م', 1000))]);
            Assert.Equal("Audit.SnapshotTooLarge", TenantAuditSnapshotSerializer.Create("products", oversized).Error?.Code);
            var fragments = ProductAuditSnapshotParts.Create(oversized);
            AssertPart(addedTag, 0, false, 0, null);
            AssertPart("Description", 0, true, 1, null);
            AssertPart("Ingredients", 0, true, 1, "");
            AssertPart("Ingredients", 1, true, 1, null);

            void AssertPart(string field, int index, bool present, int count, string? value)
            {
                var parts = fragments.Select(part => JsonSerializer.Deserialize<JsonElement>(part.BeforeState!));
                var part = Assert.Single(parts, item =>
                    item.GetProperty("field").GetString() == field && item.GetProperty("fragment").GetInt32() == index);
                Assert.Equal(present, part.GetProperty("present").GetBoolean());
                Assert.Equal(count, part.GetProperty("fragmentCount").GetInt32());
                Assert.Equal(value, part.GetProperty("value").GetString());
            }
        }

        [Fact]
        public void OversizedSnapshotsReconstructLosslesslyWithoutSplittingSurrogatePairs()
        {
            var before = string.Concat(Enumerable.Repeat("م", 1000));
            var after = new string('a', 255) + "🍵" + before;
            var changes = TenantAuditChanges.Update([new("Description", before, after)]);
            var single = TenantAuditSnapshotSerializer.Create("products", changes);
            Assert.Equal("Audit.SnapshotTooLarge", single.Error?.Code);

            var parts = ProductAuditSnapshotParts.Create(changes);
            Assert.Equal(before, string.Concat(parts.Select(part => Fragment(part.BeforeState!))));
            Assert.Equal(after, string.Concat(parts.Select(part => Fragment(part.AfterState!))));
            Assert.All(parts, part =>
            {
                Assert.True(part.BeforeState!.Length <= 4096);
                Assert.True(part.AfterState!.Length <= 4096);
            });

            static string? Fragment(string json)
            {
                using var document = JsonDocument.Parse(json);
                return document.RootElement.GetProperty("value").GetString();
            }
        }
    }
}
