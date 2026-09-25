using Nutrition.Application.Common.Models;

namespace Nutrition.Application.Common.CQRS;

/// <summary>
/// Defines a handler for an ICommand with a typed result.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// Defines a handler for an ICommand with a default Result envelope.
/// </summary>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Result>
    where TCommand : ICommand
{
}
