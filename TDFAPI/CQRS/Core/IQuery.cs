using MediatR;

namespace TDFAPI.CQRS.Core
{
    /// <summary>
    /// Marker interface for queries in CQRS pattern
    /// </summary>
    /// <typeparam name="TResult">The type of the query result</typeparam>
    public interface IQuery<out TResult> : IRequest<TResult>
    {
    }
} 