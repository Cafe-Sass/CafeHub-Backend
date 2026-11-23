using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Media
{
    public static class MediaFileValidator
    {
        private const int HeaderLength = 12;

        private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
        private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();
        private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

        public static Result<MediaContentType> Validate(MediaPurpose purpose, Stream content, long maxBytes)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!content.CanSeek)
            {
                throw new ArgumentException("The upload stream must be seekable.", nameof(content));
            }

            var size = content.Length;
            if (size == 0)
            {
                return Result.Failure<MediaContentType>(MediaErrors.InvalidFile());
            }

            if (size > maxBytes)
            {
                return Result.Failure<MediaContentType>(MediaErrors.TooLarge());
            }

            Span<byte> header = stackalloc byte[HeaderLength];
            var read = 0;
            while (read < header.Length)
            {
                var current = content.Read(header[read..]);
                if (current == 0)
                {
                    break;
                }

                read += current;
            }

            var detected = Detect(header[..read]);
            if (detected is null || !MediaPurposes.Allows(purpose, detected.Value))
            {
                return Result.Failure<MediaContentType>(MediaErrors.InvalidFile());
            }

            return Result.Success(detected.Value);
        }

        private static MediaContentType? Detect(ReadOnlySpan<byte> header)
        {
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                return MediaContentType.Jpeg;
            }

            if (header.Length >= PngSignature.Length && header[..PngSignature.Length].SequenceEqual(PngSignature))
            {
                return MediaContentType.Png;
            }

            if (header.Length >= HeaderLength
                && header[..RiffSignature.Length].SequenceEqual(RiffSignature)
                && header[8..HeaderLength].SequenceEqual(WebpSignature))
            {
                return MediaContentType.Webp;
            }

            if (header.Length >= PdfSignature.Length && header[..PdfSignature.Length].SequenceEqual(PdfSignature))
            {
                return MediaContentType.Pdf;
            }

            return null;
        }
    }
}
