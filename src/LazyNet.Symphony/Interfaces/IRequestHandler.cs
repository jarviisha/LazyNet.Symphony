using LazyNet.Symphony.Core;

namespace LazyNet.Symphony.Interfaces;

/// <summary>
/// Interface for handling requests that return a response.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for handling requests that don't return a meaningful value.
/// This is syntactic sugar for <see cref="IRequestHandler{TRequest, Unit}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <remarks>
/// Implement this interface for command handlers that perform an action
/// but don't need to return data.
/// </remarks>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
    /// <summary>
    /// Handles the request without returning a value.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await HandleAsync(request, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
