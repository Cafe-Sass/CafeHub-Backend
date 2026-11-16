namespace CafeHub.Domain.Tenants
{
    public readonly record struct ProductAddonInput(Guid? Id, string Name, decimal Price);
}
