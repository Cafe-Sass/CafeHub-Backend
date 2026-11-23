using System.Security.Cryptography;
using CafeHub.Application.Media;

namespace CafeHub.UnitTests.Media
{
    public sealed class MediaUploadServiceTests
    {
        private const long MaxBytes = 1024;

        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();

        [Fact]
        public async Task UploadAsync_WithAnAllowedImage_StoresItAndReturnsMetadata()
        {
            var storage = new FakeMediaStorage();
            var service = new MediaUploadService(storage, MaxBytes);
            using var content = new MemoryStream(Png);

            var result = await service.UploadAsync(
                MediaScope.Platform,
                MediaPurpose.PaymentProof,
                content,
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value.Key.Scope.IsPlatform);
            Assert.Equal("image/png", result.Value.ContentType);
            Assert.Equal(Png.Length, result.Value.Size);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Png)), result.Value.Sha256);
            Assert.True(await storage.ExistsAsync(result.Value.Key, CancellationToken.None));
            Assert.Equal(Png, storage.ContentOf(result.Value.Key));
        }

        [Fact]
        public async Task UploadAsync_ForATenant_KeysUnderTheTenantScope()
        {
            var tenantId = Guid.NewGuid();
            var storage = new FakeMediaStorage();
            var service = new MediaUploadService(storage, MaxBytes);
            using var content = new MemoryStream(Pdf);

            var result = await service.UploadAsync(
                MediaScope.ForTenant(tenantId),
                MediaPurpose.RefundEvidence,
                content,
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(tenantId, result.Value.Key.Scope.TenantId);
            Assert.Equal("application/pdf", result.Value.ContentType);
            Assert.Equal(Pdf.Length, result.Value.Size);
        }

        [Fact]
        public async Task UploadAsync_WithAnEmptyStream_ReturnsInvalidFileWithoutStoring()
        {
            var storage = new FakeMediaStorage();
            var service = new MediaUploadService(storage, MaxBytes);
            using var content = new MemoryStream([]);

            var result = await service.UploadAsync(
                MediaScope.Platform,
                MediaPurpose.BrandingImage,
                content,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error?.Code);
            Assert.True(storage.IsEmpty);
        }

        [Fact]
        public async Task UploadAsync_WithContentOverTheLimit_ReturnsTooLargeWithoutStoring()
        {
            var storage = new FakeMediaStorage();
            var service = new MediaUploadService(storage, MaxBytes);
            using var content = new MemoryStream(new byte[MaxBytes + 1]);

            var result = await service.UploadAsync(
                MediaScope.Platform,
                MediaPurpose.BrandingImage,
                content,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.TooLargeCode, result.Error?.Code);
            Assert.True(storage.IsEmpty);
        }

        [Fact]
        public async Task UploadAsync_WithARenamedPdfForAPaymentProof_ReturnsInvalidFileWithoutStoring()
        {
            var storage = new FakeMediaStorage();
            var service = new MediaUploadService(storage, MaxBytes);
            using var content = new MemoryStream(Pdf);

            var result = await service.UploadAsync(
                MediaScope.Platform,
                MediaPurpose.PaymentProof,
                content,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error?.Code);
            Assert.True(storage.IsEmpty);
        }

        [Fact]
        public async Task UploadAsync_WhenStorageFails_ReturnsTheStorageErrorWithoutLeftovers()
        {
            var storage = new FakeMediaStorage { SaveFailure = MediaErrors.StorageFailed() };
            var service = new MediaUploadService(storage, MaxBytes);
            using var content = new MemoryStream(Png);

            var result = await service.UploadAsync(
                MediaScope.Platform,
                MediaPurpose.BrandingImage,
                content,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.StorageFailedCode, result.Error?.Code);
            Assert.True(storage.IsEmpty);
        }

        [Fact]
        public async Task UploadAsync_WithAnUnsupportedSignature_DoesNotEchoTheContent()
        {
            var storage = new FakeMediaStorage();
            var service = new MediaUploadService(storage, MaxBytes);
            using var content = new MemoryStream("receipt-secret-content"u8.ToArray());

            var result = await service.UploadAsync(
                MediaScope.Platform,
                MediaPurpose.MenuImport,
                content,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error?.Code);
            Assert.DoesNotContain("receipt-secret", result.Error!.Message, StringComparison.Ordinal);
            Assert.True(storage.IsEmpty);
        }
    }
}
