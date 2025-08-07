namespace LazyNet.Symphony.Interfaces;

/// <summary>
/// Interface for handling domain events
/// </summary>
/// <typeparam name="TEvent">The type of event to handle</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : class
{
    /// <summary>
    /// Handles the domain event
    /// </summary>
    /// <param name="event">The event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task Handle(TEvent @event, CancellationToken cancellationToken = default);
}
