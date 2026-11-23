namespace CafeHub.Application.Media
{
    public static class MediaPurposes
    {
        private static readonly MediaContentType[] Images =
        [
            MediaContentType.Jpeg,
            MediaContentType.Png,
            MediaContentType.Webp,
        ];

        private static readonly MediaContentType[] ImagesAndPdf =
        [
            MediaContentType.Jpeg,
            MediaContentType.Png,
            MediaContentType.Webp,
            MediaContentType.Pdf,
        ];

        public static IReadOnlyList<MediaContentType> AllowedContentTypes(MediaPurpose purpose)
        {
            return purpose switch
            {
                MediaPurpose.PaymentProof => Images,
                MediaPurpose.BrandingImage => Images,
                MediaPurpose.RefundEvidence => ImagesAndPdf,
                MediaPurpose.MenuImport => ImagesAndPdf,
                _ => [],
            };
        }

        public static bool Allows(MediaPurpose purpose, MediaContentType contentType)
        {
            return AllowedContentTypes(purpose).Contains(contentType);
        }
    }
}
