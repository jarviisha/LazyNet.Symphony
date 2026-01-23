using LazyNet.Symphony.Core;

namespace LazyNet.Symphony.Interfaces;

/// <summary>
/// Marker interface for requests that return a response.
/// </summary>
/// <typeparam name="TResponse">The type of response.</typeparam>
public interface IRequest<TResponse>
{
}

/// <summary>
/// Marker interface for requests that don't return a meaningful value.
/// This is syntactic sugar for <see cref="IRequest{Unit}"/>.
/// </summary>
/// <remarks>
/// Use this interface for commands or requests that perform an action
/// but don't need to return data. The handler will return <see cref="Unit.Value"/>.
/// </remarks>
public interface IRequest : IRequest<Unit>
{
}
