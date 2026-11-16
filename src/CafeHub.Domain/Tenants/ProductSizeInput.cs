namespace CafeHub.Domain.Tenants
{
    public readonly record struct ProductSizeInput(Guid? Id, string Name, decimal Price, bool IsDefault);
}
