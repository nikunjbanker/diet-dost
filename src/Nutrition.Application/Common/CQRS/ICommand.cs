using Nutrition.Application.Common.Models;

namespace Nutrition.Application.Common.CQRS;

/// <summary>
/// Marker interface for a Command returning a typed result.
/// </summary>
public interface ICommand<out TResult>
{
}

/// <summary>
/// Marker interface for a Command returning a standard Result envelope.
/// </summary>
public interface ICommand : ICommand<Result>
{
}
