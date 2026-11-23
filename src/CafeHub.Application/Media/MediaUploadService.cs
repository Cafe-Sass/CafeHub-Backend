using System.Buffers;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Media.Contracts;

namespace CafeHub.Application.Media
{
    public sealed class MediaUploadService : IMediaUploadService
    {
        private const int CopyBufferSize = 81920;

        private readonly IMediaStorage storage;
        private readonly long maxBytes;

        public MediaUploadService(IMediaStorage storage, long maxBytes)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.maxBytes = maxBytes;
        }

        public async Task<Result<MediaUploadResult>> UploadAsync(
            MediaScope scope,
            MediaPurpose purpose,
            Stream content,
            CancellationToken cancellationToken)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            using var buffer = new MemoryStream();
            var chunk = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
            try
            {
                int read;
                while ((read = await content.ReadAsync(chunk.AsMemory(0, CopyBufferSize), cancellationToken)) > 0)
                {
                    if (buffer.Length + read > maxBytes)
                    {
                        return Result.Failure<MediaUploadResult>(MediaErrors.TooLarge());
                    }

                    buffer.Write(chunk, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(chunk);
            }

            buffer.Position = 0;
            var validation = MediaFileValidator.Validate(purpose, buffer, maxBytes);
            if (validation.Error is { } validationError)
            {
                return Result.Failure<MediaUploadResult>(validationError);
            }

            buffer.Position = 0;
            var saved = await storage.SaveAsync(MediaKey.Create(scope), buffer, cancellationToken);
            if (saved.Error is { } storageError)
            {
                return Result.Failure<MediaUploadResult>(storageError);
            }

            return Result.Success(new MediaUploadResult(
                saved.Value.Key,
                MediaContentTypes.ToContentType(validation.Value),
                saved.Value.Size,
                saved.Value.Sha256));
        }
    }
}
