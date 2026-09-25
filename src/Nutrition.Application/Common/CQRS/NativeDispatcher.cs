using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Common.Models;

namespace Nutrition.Application.Common.CQRS;

/// <summary>
/// High-performance native dispatcher powered by Microsoft.Extensions.DependencyInjection.
/// Completely eliminates MediatR/commercial licensing dependencies while guaranteeing Native AOT readiness.
/// </summary>
public class NativeDispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly ConcurrentDictionary<Type, MethodInfo> _commandHandlerCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> _queryHandlerCache = new();

    public NativeDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));
        var handler = _serviceProvider.GetRequiredService(handlerType);

        var handleMethod = _commandHandlerCache.GetOrAdd(handlerType, t =>
            t.GetMethod(nameof(ICommandHandler<ICommand<TResult>, TResult>.HandleAsync),
                new[] { commandType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException($"HandleAsync method not found on {t.Name}"));

        var resultTask = (Task<TResult>)handleMethod.Invoke(handler, new object[] { command, ct })!;
        return await resultTask;
    }

    public async Task<Result> SendAsync(ICommand command, CancellationToken ct = default)
    {
        return await SendAsync<Result>(command, ct);
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
        var handler = _serviceProvider.GetRequiredService(handlerType);

        var handleMethod = _queryHandlerCache.GetOrAdd(handlerType, t =>
            t.GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.HandleAsync),
                new[] { queryType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException($"HandleAsync method not found on {t.Name}"));

        var resultTask = (Task<TResult>)handleMethod.Invoke(handler, new object[] { query, ct })!;
        return await resultTask;
    }
}
