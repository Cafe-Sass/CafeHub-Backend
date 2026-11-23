namespace CafeHub.Application.Media.Contracts
{
    public sealed record MediaStorageResult(MediaKey Key, long Size, string Sha256);
}
