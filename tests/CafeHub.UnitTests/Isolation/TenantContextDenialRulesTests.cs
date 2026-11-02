using System.Reflection;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Tenancy;
using CafeHub.WebApi.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CafeHub.UnitTests.Isolation
{
    /// <summary>
    /// Proves the structural tenant denial rules.
    /// Note: The data-access boundary (a real database per tenant) is NOT proven by automated unit tests;
    /// it is covered by documented manual verification.
    /// </summary>
    public sealed class TenantContextDenialRulesTests
    {
        [Fact]
        public void Set_CalledTwice_ThrowsInvalidTenantContextException()
        {
            var accessor = new TenantContextAccessor();
            var tenantId = Guid.NewGuid();
            accessor.Set(TenantContext.Create(tenantId, "v1", true));

            var exception = Assert.Throws<InvalidTenantContextException>(
                () => accessor.Set(TenantContext.Create(Guid.NewGuid(), "v1", true)));

            Assert.DoesNotContain("v1", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Set_SameTenantTwice_AlsoThrowsInvalidTenantContextException()
        {
            var accessor = new TenantContextAccessor();
            var tenantId = Guid.NewGuid();
            accessor.Set(TenantContext.Create(tenantId, "v1", true));

            Assert.Throws<InvalidTenantContextException>(
                () => accessor.Set(TenantContext.Create(tenantId, "v1", true)));
        }

        [Fact]
        public void FreshAccessor_HasNoContext_GetRequiredThrows()
        {
            var accessor = new TenantContextAccessor();

            Assert.Throws<TenantContextMissingException>(() => accessor.GetRequired());
        }

        [Fact]
        public void TenantDbContext_HasNoPublicParameterlessConstructor()
        {
            var constructors = typeof(TenantDbContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            Assert.DoesNotContain(constructors, constructor => constructor.GetParameters().Length == 0);
        }

        [Fact]
        public void TenantDbContext_RequiresDbContextOptions()
        {
            var constructors = typeof(TenantDbContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            Assert.NotEmpty(constructors);
            Assert.All(constructors, constructor =>
            {
                var parameter = Assert.Single(constructor.GetParameters());
                Assert.Equal(typeof(DbContextOptions<TenantDbContext>), parameter.ParameterType);
            });
        }

        [Fact]
        public void TenantDbContext_WithoutTenantContext_FailsClosed()
        {
            using var provider = BuildTenantProvider(registration: null);

            Assert.Throws<TenantContextMissingException>(
                () => provider.GetRequiredService<TenantDbContext>());
        }

        [Fact]
        public void TenantDbContext_WhenRegistryMarksTenantUnusable_FailsClosed()
        {
            var tenantId = Guid.NewGuid();
            using var provider = BuildTenantProvider(
                new TenantRegistration(tenantId, "cafe-a", TenantLifecycleStatus.Suspended, "v1"),
                setter => setter.Set(TenantContext.Create(tenantId, "v1", true, "cafe-a")));

            Assert.Throws<InvalidTenantContextException>(
                () => provider.GetRequiredService<TenantDbContext>());
        }

        [Fact]
        public void TenantDbContext_WhenRegistryDoesNotKnowTenant_FailsClosed()
        {
            var tenantId = Guid.NewGuid();
            using var provider = BuildTenantProvider(
                registration: null,
                setter => setter.Set(TenantContext.Create(tenantId, "v1", true, "cafe-a")));

            Assert.Throws<InvalidTenantContextException>(
                () => provider.GetRequiredService<TenantDbContext>());
        }

        [Fact]
        public void OnlyTenantResolutionMiddlewareDependsOnTenantContextSetter()
        {
            var assemblies = new[]
            {
                typeof(TenantResolutionMiddleware).Assembly,
                typeof(TenantContextAccessor).Assembly,
                typeof(ITenantContextSetter).Assembly,
            };

            var violations = FindSetterDependents(assemblies.SelectMany(assembly => assembly.GetTypes()));

            Assert.Empty(violations);
        }

        [Fact]
        public void SetterDependencyGuard_DetectsASecondConsumer()
        {
            var violations = FindSetterDependents(
                new[] { typeof(TenantResolutionMiddleware), typeof(SyntheticSetterConsumer) });

            Assert.Contains(typeof(SyntheticSetterConsumer).FullName!, violations);
            Assert.DoesNotContain(typeof(TenantResolutionMiddleware).FullName!, violations);
        }

        [Fact]
        public void ApplicationCommandsAndQueries_DeclareNoTenantIdentifierMember()
        {
            var applicationAssembly = typeof(ITenantContextAccessor).Assembly;

            var violations = new List<string>();

            foreach (var type in applicationAssembly.GetTypes())
            {
                if (!IsCommandOrQuery(type))
                {
                    continue;
                }

                var declaresTenantIdentifier = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(property => property.Name.Contains("TenantId", StringComparison.OrdinalIgnoreCase))
                    || type
                        .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .SelectMany(constructor => constructor.GetParameters())
                        .Any(parameter => parameter.Name?.Contains("TenantId", StringComparison.OrdinalIgnoreCase) == true);

                if (declaresTenantIdentifier)
                {
                    violations.Add(type.FullName ?? type.Name);
                }
            }

            Assert.Empty(violations);
        }

        private static ServiceProvider BuildTenantProvider(
            TenantRegistration? registration,
            Action<ITenantContextSetter>? prepareTenant = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);
            services.Replace(ServiceDescriptor.Scoped<ITenantRegistryReader>(
                _ => new TenantRegistryReaderStub(registration)));

            var provider = services.BuildServiceProvider();

            prepareTenant?.Invoke(provider.GetRequiredService<ITenantContextSetter>());

            return provider;
        }

        private static IReadOnlyCollection<string> FindSetterDependents(IEnumerable<Type> types)
        {
            var setterType = typeof(ITenantContextSetter);
            var allowedConsumers = new HashSet<string>
            {
                typeof(TenantResolutionMiddleware).FullName!,
                typeof(TenantContextAccessor).FullName!,
            };

            var violations = new List<string>();

            foreach (var type in types)
            {
                if (type.FullName is null || IsWithinAllowedConsumer(type, allowedConsumers))
                {
                    continue;
                }

                var dependsOnSetter = type
                    .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == setterType)
                    || type
                        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .Any(field => field.FieldType == setterType)
                    || type
                        .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .Any(property => property.PropertyType == setterType)
                    || type
                        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .SelectMany(method => method.GetParameters())
                        .Any(parameter => parameter.ParameterType == setterType);

                if (dependsOnSetter)
                {
                    violations.Add(type.FullName!);
                }
            }

            return violations;
        }

        private static bool IsWithinAllowedConsumer(Type type, HashSet<string> allowedConsumers)
        {
            var current = type;

            while (current is not null)
            {
                if (current.FullName is not null && allowedConsumers.Contains(current.FullName))
                {
                    return true;
                }

                current = current.DeclaringType;
            }

            return false;
        }

        private static bool IsCommandOrQuery(Type type)
        {
            if (typeof(ICommand).IsAssignableFrom(type))
            {
                return true;
            }

            return type.GetInterfaces().Any(
                contract => contract.IsGenericType
                    && (contract.GetGenericTypeDefinition() == typeof(ICommand<>)
                        || contract.GetGenericTypeDefinition() == typeof(IQuery<>)));
        }

        private sealed class SyntheticSetterConsumer
        {
            public SyntheticSetterConsumer(ITenantContextSetter setter)
            {
                Setter = setter;
            }

            public ITenantContextSetter Setter { get; }
        }

        private sealed class TenantRegistryReaderStub(TenantRegistration? registration) : ITenantRegistryReader
        {
            public Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                return Task.FromResult(registration?.TenantId == tenantId ? registration : null);
            }

            public Task<TenantRegistration?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    registration is not null && string.Equals(registration.Slug, slug, StringComparison.OrdinalIgnoreCase)
                        ? registration
                        : null);
            }
        }
    }
}
