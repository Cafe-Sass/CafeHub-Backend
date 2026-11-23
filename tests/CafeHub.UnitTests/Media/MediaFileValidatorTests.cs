using CafeHub.Application.Media;

namespace CafeHub.UnitTests.Media
{
    public sealed class MediaFileValidatorTests
    {
        private const long MaxBytes = 5242880;

        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        private static readonly byte[] Webp = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
        private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();
        private static readonly byte[] Text = "this is not a supported upload"u8.ToArray();

        public static TheoryData<MediaPurpose, byte[], MediaContentType> AcceptedContent => new()
        {
            { MediaPurpose.PaymentProof, Jpeg, MediaContentType.Jpeg },
            { MediaPurpose.PaymentProof, Png, MediaContentType.Png },
            { MediaPurpose.BrandingImage, Webp, MediaContentType.Webp },
            { MediaPurpose.RefundEvidence, Pdf, MediaContentType.Pdf },
            { MediaPurpose.MenuImport, Pdf, MediaContentType.Pdf },
        };

        [Theory]
        [MemberData(nameof(AcceptedContent))]
        public void Validate_WithAnAllowedSignature_DetectsTheContentType(
            MediaPurpose purpose,
            byte[] content,
            MediaContentType expected)
        {
            using var stream = new MemoryStream(content);

            var result = MediaFileValidator.Validate(purpose, stream, MaxBytes);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public void Validate_WithPdfForAPaymentProof_Fails()
        {
            using var stream = new MemoryStream(Pdf);

            var result = MediaFileValidator.Validate(MediaPurpose.PaymentProof, stream, MaxBytes);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error?.Code);
        }

        [Fact]
        public void Validate_WithAnUnknownSignature_Fails()
        {
            using var stream = new MemoryStream(Text);

            var result = MediaFileValidator.Validate(MediaPurpose.MenuImport, stream, MaxBytes);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error?.Code);
        }

        [Fact]
        public void Validate_WithAnEmptyStream_Fails()
        {
            using var stream = new MemoryStream([]);

            var result = MediaFileValidator.Validate(MediaPurpose.BrandingImage, stream, MaxBytes);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error?.Code);
        }

        [Fact]
        public void Validate_WithContentOverTheLimit_Fails()
        {
            using var stream = new MemoryStream(new byte[1025]);

            var result = MediaFileValidator.Validate(MediaPurpose.BrandingImage, stream, 1024);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.TooLargeCode, result.Error?.Code);
        }

        [Fact]
        public void Validate_WithAnUnknownPurpose_Fails()
        {
            using var stream = new MemoryStream(Png);

            var result = MediaFileValidator.Validate((MediaPurpose)99, stream, MaxBytes);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error?.Code);
        }

        [Fact]
        public void Validate_WithANullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MediaFileValidator.Validate(MediaPurpose.BrandingImage, null!, MaxBytes));
        }

        [Fact]
        public void Validate_WithANonSeekableStream_Throws()
        {
            using var stream = new NonSeekableStream(Png);

            Assert.Throws<ArgumentException>(() =>
                MediaFileValidator.Validate(MediaPurpose.BrandingImage, stream, MaxBytes));
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] content)
                : base(content)
            {
            }

            public override bool CanSeek => false;
        }
    }
}
