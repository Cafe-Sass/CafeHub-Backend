using System.Text.Json;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.UnitTests
{
    public sealed class TenantAuditWriterTests
    {
        private static readonly DateTimeOffset RecordedAt = DateTimeOffset.Parse("2026-10-27T12:30:00Z");
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        private static (TenantAuditWriter Writer, TenantDbContext Context) CreateWriter(
            bool isAuthenticated = false,
            Guid? currentUserId = null)
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=unit-test")
                .Options;
            var context = new TenantDbContext(options);
            var writer = new TenantAuditWriter(
                new StubCurrentTenantUser(isAuthenticated, currentUserId),
                new FixedTimeProvider(RecordedAt),
                context,
                new StubTenantContextAccessor());
            return (writer, context);
        }

        [Fact]
        public async Task WriteAsync_CapturesAuthenticatedUserAndTenantRecordFields()
        {
            var userId = Guid.NewGuid();
            var (writer, context) = CreateWriter(true, userId);
            using (context)
            {
                var result = await writer.WriteAsync(new TenantAuditEntry
                {
                    Action = "tenant.login.succeeded",
                    TargetType = "tenant_user",
                    TargetId = userId,
                    Reason = TenantAuditReason.InvalidCredentials,
                }, CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.NotEqual(Guid.Empty, record.Id);
                Assert.Equal(RecordedAt, record.RecordedAt);
                Assert.Equal(userId, record.ActorUserId);
                Assert.Equal(TenantAuditRecordKind.User, record.ActorKind);
                Assert.Equal("tenant.login.succeeded", record.ActionCode);
                Assert.Equal("tenant_user", record.SubjectType);
                Assert.Equal(userId, record.SubjectId);
                Assert.Null(record.BeforeState);
                Assert.Null(record.AfterState);
            }
        }

        [Fact]
        public async Task WriteAsync_UsesKnownActorOrSystemWhenNoAuthenticatedUserExists()
        {
            var knownUserId = Guid.NewGuid();
            var (knownActorWriter, knownActorContext) = CreateWriter();
            using (knownActorContext)
            {
                var result = await knownActorWriter.WriteAsync(new TenantAuditEntry
                {
                    Action = "tenant.login.failed",
                    TargetType = "tenant_user",
                    TargetId = knownUserId,
                    ActorUserId = knownUserId,
                }, CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(knownActorContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Equal(TenantAuditRecordKind.User, record.ActorKind);
                Assert.Equal(knownUserId, record.ActorUserId);
            }

            var (unknownActorWriter, unknownActorContext) = CreateWriter();
            using (unknownActorContext)
            {
                var result = await unknownActorWriter.WriteAsync(new TenantAuditEntry
                {
                    Action = "tenant.login.failed",
                    TargetType = "tenant_user",
                }, CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(unknownActorContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Equal(TenantAuditRecordKind.System, record.ActorKind);
                Assert.Null(record.ActorUserId);
            }
        }

        [Fact]
        public void TenantAuditEntry_ContainsOnlyAuditMetadataAndNoSecretBearingMembers()
        {
            var properties = typeof(TenantAuditEntry).GetProperties().Select(property => property.Name).Order().ToArray();

            Assert.Equal(new[] { "Action", "ActorUserId", "Changes", "Reason", "TargetId", "TargetType" }.Order(), properties);
            Assert.DoesNotContain(properties, property => property.Contains("email", StringComparison.OrdinalIgnoreCase)
                || property.Contains("password", StringComparison.OrdinalIgnoreCase)
                || property.Contains("hash", StringComparison.OrdinalIgnoreCase)
                || property.Contains("token", StringComparison.OrdinalIgnoreCase)
                || property.Contains("secret", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task WriteAsync_RejectsActionCodesOutsideTheTableContract()
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(new TenantAuditEntry
                {
                    Action = "Tenant login",
                    TargetType = "tenant_user",
                }, CancellationToken.None);

                Assert.True(result.IsFailure);
                Assert.Empty(context.ChangeTracker.Entries<TenantAuditRecord>());
            }
        }

        [Fact]
        public async Task WriteAsync_ChangesForUnknownTarget_ReturnsFailureWithoutAddingRecord()
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("unknown_target", TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange("Name", "old", "new"),
                })), CancellationToken.None);

                Assert.True(result.IsFailure);
                Assert.Equal("Audit.UnsupportedSnapshotTarget", result.Error!.Code);
                Assert.Empty(context.ChangeTracker.Entries<TenantAuditRecord>());
            }
        }

        [Fact]
        public async Task WriteAsync_InvalidDuplicateSnapshotFields_ReturnsFailureWithoutAddingRecord()
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("branches", TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange("Name", "old", "new"),
                    new TenantAuditChanges.FieldChange("Name", "previous", "current"),
                })), CancellationToken.None);

                Assert.True(result.IsFailure);
                Assert.Equal("Audit.InvalidSnapshotChanges", result.Error!.Code);
                Assert.Empty(context.ChangeTracker.Entries<TenantAuditRecord>());
            }
        }

        [Fact]
        public async Task WriteAsync_UnknownTargetWithoutChanges_PreservesMetadataOnlyBehavior()
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("tenant_user"), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Null(record.BeforeState);
                Assert.Null(record.AfterState);
            }
        }

        [Fact]
        public async Task WriteAsync_UsesOnlyExactAllowListedKeysForEachTarget()
        {
            var cafeChanges = TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange("Timezone", null, "Africa/Cairo"),
                new TenantAuditChanges.FieldChange("CafeName", null, "Cafe"),
                new TenantAuditChanges.FieldChange("PrimaryColor", null, "#112233"),
                new TenantAuditChanges.FieldChange("SecondaryColor", null, "#223344"),
                new TenantAuditChanges.FieldChange("AccentColor", null, "#334455"),
                new TenantAuditChanges.FieldChange("LogoMediaReference", null, "logo-ref"),
                new TenantAuditChanges.FieldChange("ContactAddress", null, "address"),
                new TenantAuditChanges.FieldChange("cafe_name", null, "alias"),
            });
            var (cafeWriter, cafeContext) = CreateWriter();
            using (cafeContext)
            {
                Assert.True((await cafeWriter.WriteAsync(Entry("cafe_settings", cafeChanges), CancellationToken.None)).IsSuccess);
                var record = Assert.Single(cafeContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                using var json = JsonDocument.Parse(record.AfterState!);
                Assert.Equal(
                    new[] { "AccentColor", "CafeName", "PrimaryColor", "SecondaryColor", "Timezone" },
                    json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            }

            var branchChanges = TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange("LifecycleStatus", null, "Active"),
                new TenantAuditChanges.FieldChange("Name", null, "Branch"),
                new TenantAuditChanges.FieldChange("OpeningHoursSummary", null, "Daily"),
                new TenantAuditChanges.FieldChange("Address", null, "excluded"),
                new TenantAuditChanges.FieldChange("ContactPhone", null, "excluded"),
            });
            var (branchWriter, branchContext) = CreateWriter();
            using (branchContext)
            {
                Assert.True((await branchWriter.WriteAsync(Entry("branches", branchChanges), CancellationToken.None)).IsSuccess);
                var record = Assert.Single(branchContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                using var json = JsonDocument.Parse(record.AfterState!);
                Assert.Equal(
                    new[] { "LifecycleStatus", "Name", "OpeningHoursSummary" },
                    json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            }
        }

        [Theory]
        [InlineData("Password")]
        [InlineData("PasswordHash")]
        [InlineData("password_hash")]
        [InlineData("AccessToken")]
        [InlineData("ACCESS-TOKEN")]
        [InlineData("Refresh Token")]
        [InlineData("Secret")]
        [InlineData("ApiKey")]
        [InlineData("api-key")]
        [InlineData("Credential")]
        [InlineData("ConnectionString")]
        [InlineData("connection_string")]
        [InlineData("Email")]
        [InlineData("ContactEmail")]
        [InlineData("Contact_Email")]
        [InlineData("Phone")]
        [InlineData("ContactPhone")]
        [InlineData("contact-phone")]
        public async Task WriteAsync_DenyListPrecedesAllowList_AndNormalizesCaseAndSeparators(string name)
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange(name, null, "sensitive"),
                    new TenantAuditChanges.FieldChange("Name", null, "allowed"),
                });

                var result = await writer.WriteAsync(Entry("branches", changes), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                using var json = JsonDocument.Parse(record.AfterState!);
                Assert.Equal("allowed", json.RootElement.GetProperty("Name").GetString());
                Assert.Single(json.RootElement.EnumerateObject());
            }
        }

        [Fact]
        public async Task WriteAsync_DoesNotMapUnknownNamesOrAliasesToCanonicalFields()
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("branches", TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange("BranchName", null, "not mapped"),
                    new TenantAuditChanges.FieldChange("branch_name", null, "not mapped"),
                    new TenantAuditChanges.FieldChange("ContactEmail", null, "denied"),
                })), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Null(record.BeforeState);
                Assert.Null(record.AfterState);
            }
        }

        [Fact]
        public async Task WriteAsync_UpdateIncludesChangedFieldsOnlyAndPreservesChangedNull()
        {
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("branches", TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange("Name", "old", "new"),
                    new TenantAuditChanges.FieldChange("OpeningHoursSummary", "same", "same"),
                    new TenantAuditChanges.FieldChange("LifecycleStatus", "Active", null),
                })), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                using var before = JsonDocument.Parse(record.BeforeState!);
                using var after = JsonDocument.Parse(record.AfterState!);
                Assert.Equal("old", before.RootElement.GetProperty("Name").GetString());
                Assert.Equal("Active", before.RootElement.GetProperty("LifecycleStatus").GetString());
                Assert.Equal("new", after.RootElement.GetProperty("Name").GetString());
                Assert.Equal(JsonValueKind.Null, after.RootElement.GetProperty("LifecycleStatus").ValueKind);
                Assert.False(before.RootElement.TryGetProperty("OpeningHoursSummary", out _));
                Assert.False(after.RootElement.TryGetProperty("OpeningHoursSummary", out _));
            }
        }

        [Fact]
        public async Task WriteAsync_CreateUsesAfterStateOnly_AndEmptyDiffHasNullStates()
        {
            var (createWriter, createContext) = CreateWriter();
            using (createContext)
            {
                var result = await createWriter.WriteAsync(Entry("branches", TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange("Name", null, "new branch"),
                    new TenantAuditChanges.FieldChange("Address", null, "excluded"),
                })), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(createContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Null(record.BeforeState);
                using var after = JsonDocument.Parse(record.AfterState!);
                Assert.Equal("new branch", after.RootElement.GetProperty("Name").GetString());
                Assert.Single(after.RootElement.EnumerateObject());
            }

            var (emptyWriter, emptyContext) = CreateWriter();
            using (emptyContext)
            {
                var result = await emptyWriter.WriteAsync(Entry("branches", TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange("Name", "same", "same"),
                })), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(emptyContext.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.Null(record.BeforeState);
                Assert.Null(record.AfterState);
            }
        }

        [Fact]
        public async Task WriteAsync_SortsKeysIndependentlyOfInputOrder()
        {
            var first = await WriteCreateSnapshot(new[] { "OpeningHoursSummary", "Name", "LifecycleStatus" });
            var second = await WriteCreateSnapshot(new[] { "LifecycleStatus", "OpeningHoursSummary", "Name" });

            Assert.Equal(first, second);
        }

        [Fact]
        public async Task WriteAsync_TruncatesValuesAndKeepsJsonValidWithinStateLimits()
        {
            var longValue = string.Concat(Enumerable.Repeat("Cafe 😀 ", 2000));
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("branches", TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange("Name", longValue, "new"),
                    new TenantAuditChanges.FieldChange("OpeningHoursSummary", "old", longValue),
                })), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.True(record.BeforeState!.Length <= 4096);
                Assert.True(record.AfterState!.Length <= 4096);
                using var before = JsonDocument.Parse(record.BeforeState!);
                using var after = JsonDocument.Parse(record.AfterState!);
                var truncatedName = before.RootElement.GetProperty("Name").GetString()!;
                var markerIndex = truncatedName.LastIndexOf("...[truncated]", StringComparison.Ordinal);
                Assert.True(markerIndex > 0);
                Assert.False(char.IsHighSurrogate(truncatedName[markerIndex - 1]));
                Assert.Contains("...[truncated]", after.RootElement.GetProperty("OpeningHoursSummary").GetString()!);
                Assert.Equal("new", after.RootElement.GetProperty("Name").GetString());
            }
        }

        [Fact]
        public async Task WriteAsync_EscapesJsonCharactersAndKeepsEachStateWithinItsOwnLimit()
        {
            const string oldValue = "quote\" slash\\ line\n tab\t";
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("branches", TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange("Name", new string('x', 12000) + oldValue, "short"),
                    new TenantAuditChanges.FieldChange("OpeningHoursSummary", oldValue, "next\"\\ line\n tab\t"),
                })), CancellationToken.None);

                Assert.True(result.IsSuccess);
                var record = Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity;
                Assert.True(record.BeforeState!.Length <= 4096);
                Assert.True(record.AfterState!.Length <= 4096);
                using var before = JsonDocument.Parse(record.BeforeState!);
                using var after = JsonDocument.Parse(record.AfterState!);
                Assert.Contains("...[truncated]", before.RootElement.GetProperty("Name").GetString()!);
                Assert.Equal("short", after.RootElement.GetProperty("Name").GetString());
                Assert.Equal(oldValue, before.RootElement.GetProperty("OpeningHoursSummary").GetString());
                Assert.Equal("next\"\\ line\n tab\t", after.RootElement.GetProperty("OpeningHoursSummary").GetString());
            }
        }

        [Fact]
        public async Task WriteAsync_TruncatesMultipleLongFieldsDeterministicallyWithoutDroppingKeys()
        {
            var value = new string('x', 10000);
            var first = await WriteLargeBranchSnapshot(new[] { "Name", "OpeningHoursSummary", "LifecycleStatus" }, value);
            var second = await WriteLargeBranchSnapshot(new[] { "LifecycleStatus", "Name", "OpeningHoursSummary" }, value);

            Assert.Equal(first, second);
            using var json = JsonDocument.Parse(first);
            Assert.Equal(3, json.RootElement.EnumerateObject().Count());
            Assert.All(json.RootElement.EnumerateObject(), property =>
                Assert.Contains("...[truncated]", property.Value.GetString()!));
            Assert.True(first.Length <= 4096);
        }

        [Fact]
        public void Create_WhenEvenMinimumRepresentationCannotFit_ReturnsFailure()
        {
            var changes = TenantAuditChanges.Create(new[]
            {
                new TenantAuditChanges.FieldChange("Name", null, new string('x', 100)),
            });

            var result = TenantAuditSnapshotSerializer.Create(
                "branches",
                changes,
                maximumStateLength: 1);

            Assert.True(result.IsFailure);
            Assert.Equal("Audit.SnapshotTooLarge", result.Error!.Code);
        }

        private static TenantAuditEntry Entry(string targetType, TenantAuditChanges? changes = null) => new()
        {
            Action = "branches.updated.succeeded",
            TargetType = targetType,
            Changes = changes,
        };

        private static async Task<string> WriteCreateSnapshot(IEnumerable<string> fieldNames)
        {
            var fields = fieldNames.Select(name => new TenantAuditChanges.FieldChange(
                name,
                null,
                name switch
                {
                    "Name" => "Branch",
                    "OpeningHoursSummary" => "Daily",
                    _ => "Active",
                })).ToArray();
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("branches", TenantAuditChanges.Create(fields)), CancellationToken.None);
                Assert.True(result.IsSuccess);
                return Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity.AfterState!;
            }
        }

        private static async Task<string> WriteLargeBranchSnapshot(IEnumerable<string> fieldNames, string value)
        {
            var fields = fieldNames.Select(name => new TenantAuditChanges.FieldChange(name, null, value)).ToArray();
            var (writer, context) = CreateWriter();
            using (context)
            {
                var result = await writer.WriteAsync(Entry("branches", TenantAuditChanges.Create(fields)), CancellationToken.None);
                Assert.True(result.IsSuccess);
                return Assert.Single(context.ChangeTracker.Entries<TenantAuditRecord>()).Entity.AfterState!;
            }
        }

        private sealed class StubCurrentTenantUser(bool isAuthenticated, Guid? userId) : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; } = isAuthenticated;

            public Guid? UserId { get; } = userId;

            public Guid? TenantId => IsAuthenticated ? TenantIdValue : null;

            public IReadOnlyCollection<string> Roles => Array.Empty<string>();

            private static Guid TenantIdValue => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        }

        private sealed class StubTenantContextAccessor : ITenantContextAccessor
        {
            public TenantContext GetRequired() => TenantContext.Create(TenantId, "tenant-secret-reference", true);
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
