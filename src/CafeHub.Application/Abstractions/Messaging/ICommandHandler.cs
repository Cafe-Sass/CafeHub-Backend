using CafeHub.Application.Abstractions.Results;
using MediatR;

namespace CafeHub.Application.Abstractions.Messaging
{
    public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
        where TCommand : ICommand
    {
    }
}
