namespace Nutrition.Application.Common.CQRS;

/// <summary>
/// Defines a handler for an IQuery with a typed result.
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
