using Nutrition.Application.Common.Models;

namespace Nutrition.Application.Common.CQRS;

/// <summary>
/// Native in-process CQRS dispatcher decoupling presentation controllers from handler implementations.
/// Pure Microsoft.Extensions.DependencyInjection foundation with zero third-party licensing baggage.
/// </summary>
public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task<Result> SendAsync(ICommand command, CancellationToken ct = default);
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
