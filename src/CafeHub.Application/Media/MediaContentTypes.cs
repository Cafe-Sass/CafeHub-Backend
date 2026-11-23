namespace CafeHub.Application.Media
{
    public static class MediaContentTypes
    {
        public static string ToContentType(MediaContentType contentType)
        {
            return contentType switch
            {
                MediaContentType.Jpeg => "image/jpeg",
                MediaContentType.Png => "image/png",
                MediaContentType.Webp => "image/webp",
                MediaContentType.Pdf => "application/pdf",
                _ => throw new ArgumentOutOfRangeException(nameof(contentType)),
            };
        }
    }
}
