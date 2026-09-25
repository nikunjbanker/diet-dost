namespace Nutrition.Application.Common.CQRS;

/// <summary>
/// Marker interface for a Query returning a typed result.
/// </summary>
public interface IQuery<out TResult>
{
}
