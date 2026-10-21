using CafeHub.Application.Abstractions.Results;
using MediatR;

namespace CafeHub.Application.Abstractions.Messaging
{
    public interface ICommand : IRequest<Result>
    {
    }
}
