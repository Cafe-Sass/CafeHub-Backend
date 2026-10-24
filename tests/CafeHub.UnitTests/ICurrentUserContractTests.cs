using CafeHub.Application.Identity;

namespace CafeHub.UnitTests
{
    public sealed class ICurrentUserContractTests
    {
        [Fact]
        public void ApplicationAssembly_DoesNotReferenceAspNetCore()
        {
            var referencedAssemblyNames = typeof(ICurrentUser).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToList();

            Assert.DoesNotContain(
                referencedAssemblyNames,
                name => name is not null && name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        }

        [Fact]
        public void CurrentUserContract_ExposesOnlyPrimitiveMembers()
        {
            var members = typeof(ICurrentUser).GetProperties();

            var propertyTypes = members.Select(member => member.PropertyType).ToList();

            Assert.Equal(3, propertyTypes.Count);
            Assert.Contains(propertyTypes, propertyType => propertyType == typeof(bool));
            Assert.Contains(propertyTypes, propertyType => propertyType == typeof(Guid?));
            Assert.Contains(propertyTypes, propertyType => propertyType == typeof(IReadOnlyCollection<string>));
        }
    }
}
