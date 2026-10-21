using CafeHub.Application.Abstractions.Results;
using MediatR;

namespace CafeHub.Application.Abstractions.Messaging
{
    public interface IQuery<TResponse> : IRequest<Result<TResponse>>
    {
    }
}
