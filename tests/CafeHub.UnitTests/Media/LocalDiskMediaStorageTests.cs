using System.Security.Cryptography;
using System.Text;
using CafeHub.Application.Media;
using CafeHub.Infrastructure.Media;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.UnitTests.Media
{
    public sealed class LocalDiskMediaStorageTests : IDisposable
    {
        private readonly string rootPath;

        public LocalDiskMediaStorageTests()
        {
            rootPath = Path.Combine(Path.GetTempPath(), "cafehub-media-tests", Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }

        private LocalDiskMediaStorage CreateStorage(int maxBytes = MediaOptions.DefaultMaxBytes)
        {
            var options = new MediaOptions { RootPath = rootPath, MaxBytes = maxBytes };

            return new LocalDiskMediaStorage(options, NullLogger<LocalDiskMediaStorage>.Instance);
        }

        private static MemoryStream Content(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value));
        }

        private IReadOnlyList<string> StoredFiles()
        {
            return Directory.Exists(rootPath)
                ? Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
        }

        [Fact]
        public async Task SaveAsync_WritesTheFileUnderTheKeyAndReportsItsHash()
        {
            var storage = CreateStorage();
            var key = MediaKey.Create(MediaScope.Platform);
            var payload = Encoding.UTF8.GetBytes("bank transfer receipt");

            var result = await storage.SaveAsync(key, new MemoryStream(payload), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(payload.Length, result.Value.Size);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(payload)), result.Value.Sha256);
            var storedPath = Path.Combine(rootPath, "platform", key.Id);
            Assert.True(File.Exists(storedPath));
            Assert.Equal(payload, await File.ReadAllBytesAsync(storedPath));
        }

        [Fact]
        public async Task SaveAsync_ForATenantScope_WritesUnderTheTenantFolder()
        {
            var storage = CreateStorage();
            var tenantId = Guid.NewGuid();
            var key = MediaKey.Create(MediaScope.ForTenant(tenantId));

            await storage.SaveAsync(key, Content("logo"), CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(rootPath, "tenants", tenantId.ToString("D"), key.Id)));
        }

        [Fact]
        public async Task SaveAsync_TwiceForTheSameKey_DoesNotOverwrite()
        {
            var storage = CreateStorage();
            var key = MediaKey.Create(MediaScope.Platform);
            await storage.SaveAsync(key, Content("first"), CancellationToken.None);

            var result = await storage.SaveAsync(key, Content("second"), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(MediaErrors.KeyExistsCode, result.Error!.Code);
            await using var stream = await storage.OpenAsync(key, CancellationToken.None);
            using var reader = new StreamReader(stream!);
            Assert.Equal("first", await reader.ReadToEndAsync());
        }

        [Fact]
        public async Task SaveAsync_WithContentOverTheLimit_FailsAndLeavesNoFile()
        {
            var storage = CreateStorage(MediaOptions.MinMaxBytes);
            var key = MediaKey.Create(MediaScope.Platform);
            var oversized = new byte[MediaOptions.MinMaxBytes + 1];

            var result = await storage.SaveAsync(key, new MemoryStream(oversized), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(MediaErrors.TooLargeCode, result.Error!.Code);
            Assert.False(await storage.ExistsAsync(key, CancellationToken.None));
            Assert.Empty(StoredFiles());
        }

        [Fact]
        public async Task SaveAsync_WithContentAtTheLimit_Succeeds()
        {
            var storage = CreateStorage(MediaOptions.MinMaxBytes);
            var key = MediaKey.Create(MediaScope.Platform);
            var content = new byte[MediaOptions.MinMaxBytes];

            var result = await storage.SaveAsync(key, new MemoryStream(content), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(MediaOptions.MinMaxBytes, result.Value.Size);
        }

        [Fact]
        public async Task SaveAsync_WithEmptyContent_FailsAndLeavesNoFile()
        {
            var storage = CreateStorage();
            var key = MediaKey.Create(MediaScope.Platform);

            var result = await storage.SaveAsync(key, new MemoryStream(), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(MediaErrors.InvalidFileCode, result.Error!.Code);
            Assert.Empty(StoredFiles());
        }

        [Fact]
        public async Task OpenAsync_ForAMissingKey_ReturnsNull()
        {
            var storage = CreateStorage();

            var stream = await storage.OpenAsync(MediaKey.Create(MediaScope.Platform), CancellationToken.None);

            Assert.Null(stream);
        }

        [Fact]
        public async Task ExistsAsync_And_DeleteAsync_ReflectTheStoredFile()
        {
            var storage = CreateStorage();
            var key = MediaKey.Create(MediaScope.Platform);
            await storage.SaveAsync(key, Content("receipt"), CancellationToken.None);

            Assert.True(await storage.ExistsAsync(key, CancellationToken.None));
            Assert.True(await storage.DeleteAsync(key, CancellationToken.None));
            Assert.False(await storage.DeleteAsync(key, CancellationToken.None));
            Assert.False(await storage.ExistsAsync(key, CancellationToken.None));
        }

        [Fact]
        public async Task SaveAsync_WhenTheContentStreamFails_LeavesNoFile()
        {
            var storage = CreateStorage();
            var key = MediaKey.Create(MediaScope.Platform);

            var result = await storage.SaveAsync(key, new FailingStream(), CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(MediaErrors.StorageFailedCode, result.Error!.Code);
            Assert.Empty(StoredFiles());
        }

        private sealed class FailingStream : Stream
        {
            private bool written;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new IOException("The content stream failed.");
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (written)
                {
                    throw new IOException("The content stream failed.");
                }

                written = true;
                buffer.Span[0] = 0x01;

                return ValueTask.FromResult(1);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
