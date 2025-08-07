namespace LazyNet.Symphony.Interfaces;

/// <summary>
/// Interface for sending requests to their corresponding handlers
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request to its handler
    /// </summary>
    /// <typeparam name="TResponse">The type of response</typeparam>
    /// <param name="request">The request to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The response from the handler</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event to all registered handlers
    /// </summary>
    /// <typeparam name="TEvent">The type of event</typeparam>
    /// <param name="event">The event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;
}
