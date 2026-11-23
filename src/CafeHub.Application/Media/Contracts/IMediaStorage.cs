using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Media.Contracts
{
    public interface IMediaStorage
    {
        Task<Result<MediaStorageResult>> SaveAsync(
            MediaKey key,
            Stream content,
            CancellationToken cancellationToken);

        Task<Stream?> OpenAsync(MediaKey key, CancellationToken cancellationToken);

        Task<bool> ExistsAsync(MediaKey key, CancellationToken cancellationToken);

        Task<bool> DeleteAsync(MediaKey key, CancellationToken cancellationToken);
    }
}
