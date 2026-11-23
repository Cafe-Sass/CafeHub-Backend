using System.Security.Cryptography;
using System.Text;
using CafeHub.Application.Media;

namespace CafeHub.UnitTests.Media
{
    public sealed class FakeMediaStorageTests
    {
        private static Stream Content(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value));
        }

        [Fact]
        public async Task SaveAsync_StoresTheContentAndReportsSizeAndHash()
        {
            var storage = new FakeMediaStorage();
            var key = MediaKey.Create(MediaScope.Platform);
            var payload = Encoding.UTF8.GetBytes("payment proof");

            var result = await storage.SaveAsync(key, new MemoryStream(payload), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(payload.Length, result.Value.Size);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(payload)), result.Value.Sha256);
            Assert.Equal(key.Value, result.Value.Key.Value);
            Assert.True(await storage.ExistsAsync(key, CancellationToken.None));
        }

        [Fact]
        public async Task OpenAsync_ReturnsTheStoredContent()
        {
            var storage = new FakeMediaStorage();
            var key = MediaKey.Create(MediaScope.Platform);
            await storage.SaveAsync(key, Content("image"), CancellationToken.None);

            await using var stream = await storage.OpenAsync(key, CancellationToken.None);

            Assert.NotNull(stream);
            using var reader = new StreamReader(stream!);
            Assert.Equal("image", await reader.ReadToEndAsync());
        }

        [Fact]
        public async Task OpenAsync_ForAMissingKey_ReturnsNull()
        {
            var storage = new FakeMediaStorage();

            var stream = await storage.OpenAsync(MediaKey.Create(MediaScope.Platform), CancellationToken.None);

            Assert.Null(stream);
        }

        [Fact]
        public async Task SaveAsync_TwiceForTheSameKey_DoesNotOverwrite()
        {
            var storage = new FakeMediaStorage();
            var key = MediaKey.Create(MediaScope.Platform);
            await storage.SaveAsync(key, Content("first"), CancellationToken.None);

            var result = await storage.SaveAsync(key, Content("second"), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(MediaErrors.KeyExistsCode, result.Error!.Code);
            Assert.Equal("first"u8.ToArray(), storage.ContentOf(key));
        }

        [Fact]
        public async Task DeleteAsync_RemovesTheFile()
        {
            var storage = new FakeMediaStorage();
            var key = MediaKey.Create(MediaScope.ForTenant(Guid.NewGuid()));
            await storage.SaveAsync(key, Content("evidence"), CancellationToken.None);

            Assert.True(await storage.DeleteAsync(key, CancellationToken.None));
            Assert.False(await storage.DeleteAsync(key, CancellationToken.None));
            Assert.False(await storage.ExistsAsync(key, CancellationToken.None));
            Assert.Equal(0, storage.Count);
        }

        [Fact]
        public async Task SaveAsync_WithEmptyContent_LeavesNoFile()
        {
            var storage = new FakeMediaStorage();
            var key = MediaKey.Create(MediaScope.Platform);

            var result = await storage.SaveAsync(key, new MemoryStream(), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error!.Code);
            Assert.False(await storage.ExistsAsync(key, CancellationToken.None));
        }
    }
}
