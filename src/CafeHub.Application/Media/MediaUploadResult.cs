namespace CafeHub.Application.Media
{
    public sealed record MediaUploadResult(MediaKey Key, string ContentType, long Size, string Sha256);
}
