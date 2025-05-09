using MediatR;

namespace TDFAPI.CQRS.Core
{
    /// <summary>
    /// Marker interface for commands in CQRS pattern
    /// </summary>
    /// <typeparam name="TResult">The type of the command result</typeparam>
    public interface ICommand<out TResult> : IRequest<TResult>
    {
    }

    /// <summary>
    /// Marker interface for commands with no return value
    /// </summary>
    public interface ICommand : IRequest<Unit>
    {
    }
} 