namespace CafeHub.Infrastructure.Tenancy
{
    internal interface ISecretReferenceResolver
    {
        string Resolve(string secretReference);
    }
}
