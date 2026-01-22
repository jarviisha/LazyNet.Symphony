using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using LazyNet.Symphony.Exceptions;
using LazyNet.Symphony.Interfaces;


namespace LazyNet.Symphony.Core;

/// <summary>
/// High-performance implementation of the mediator pattern that provides request/response 
/// and publish/subscribe messaging with pipeline support and advanced caching.
/// </summary>
/// <remarks>
/// This mediator implementation uses compiled delegates and type caching for optimal performance.
/// It supports:
/// - Request/Response patterns with pipeline behaviors
/// - Event publishing with multiple handlers
/// - Dependency injection integration
/// - Exception handling with detailed context
/// </remarks>
public class Mediator : IMediator
{
    /// <summary>
    /// Service provider used for dependency injection and service resolution.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;
    
    /// <summary>
    /// Cache for frequently used generic handler types to avoid repeated MakeGenericType calls
    /// and improve performance during request processing.
    /// Key is a ValueTuple of (RequestType, ResponseType) for efficient lookups.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), Type> _handlerTypeCache = new();

    /// <summary>
    /// Cache for frequently used generic behavior types to avoid repeated MakeGenericType calls
    /// and improve performance during pipeline construction.
    /// Key is a ValueTuple of (RequestType, ResponseType) for efficient lookups.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), Type> _behaviorTypeCache = new();
    
    /// <summary>
    /// Cache for frequently used generic event handler types to avoid repeated MakeGenericType calls
    /// and improve performance during event publishing.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Type> _eventHandlerTypeCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection and service resolution.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
    public Mediator(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Sends a request to the appropriate handler and returns the response.
    /// The request is processed through the pipeline behaviors before reaching the handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response expected from the handler.</typeparam>
    /// <param name="request">The request object to be processed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the handler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="HandlerNotFoundException">Thrown when no handler is found for the request type.</exception>
    /// <remarks>
    /// The method processes the request through registered pipeline behaviors in FIFO order
    /// (First In, First Out) before executing the actual handler.
    /// </remarks>
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var requestType = request.GetType();
        
        // Use cached generic types for better performance
        var handlerType = GetOrCreateHandlerType(requestType, typeof(TResponse));
        var behaviorType = GetOrCreateBehaviorType(requestType, typeof(TResponse));
        
        var handler = _serviceProvider.GetService(handlerType);
        if (handler == null)
        {
            throw new HandlerNotFoundException(requestType, handlerType)
                .WithContext("RequestInstance", request.ToString());
        }

        var behaviors = GetValidBehaviors(behaviorType);

        // Build and execute pipeline
        var pipeline = BuildPipeline<TResponse>(handler, request, behaviors, cancellationToken);
        return await pipeline();
    }

    /// <summary>
    /// Publishes an event to all registered event handlers.
    /// All handlers are executed sequentially in the order they were registered.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to be published.</typeparam>
    /// <param name="event">The event object to be published to handlers.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="event"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Event handlers are executed sequentially in FIFO order (First In, First Out) based on their registration order.
    /// This ensures predictable execution order and better resource management.
    /// </para>
    /// <para>
    /// Any exceptions thrown by handlers will bubble up to the caller for application-level handling.
    /// </para>
    /// </remarks>
    public async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        cancellationToken.ThrowIfCancellationRequested();

        var eventType = @event.GetType();
        var handlerType = GetOrCreateEventHandlerType(eventType);
        
        var handlers = GetValidEventHandlers(handlerType);
        if (handlers.Length == 0) return; // No handlers registered

        // Execute handlers sequentially in registration order (FIFO)
        await ExecuteEventHandlers(handlers, @event, cancellationToken);
    }

    /// <summary>
    /// Gets or creates cached handler type for better performance.
    /// Uses ValueTuple as key to avoid MakeGenericType overhead for cache lookups.
    /// </summary>
    private static Type GetOrCreateHandlerType(Type requestType, Type responseType)
    {
        var key = (RequestType: requestType, ResponseType: responseType);
        return _handlerTypeCache.GetOrAdd(key, k =>
            typeof(IRequestHandler<,>).MakeGenericType(k.RequestType, k.ResponseType));
    }

    /// <summary>
    /// Gets or creates cached behavior type for better performance.
    /// Uses ValueTuple as key to avoid MakeGenericType overhead for cache lookups.
    /// </summary>
    private static Type GetOrCreateBehaviorType(Type requestType, Type responseType)
    {
        var key = (RequestType: requestType, ResponseType: responseType);
        return _behaviorTypeCache.GetOrAdd(key, k =>
            typeof(IPipelineBehavior<,>).MakeGenericType(k.RequestType, k.ResponseType));
    }

    /// <summary>
    /// Gets or creates cached event handler type for better performance
    /// </summary>
    private static Type GetOrCreateEventHandlerType(Type eventType)
    {
        return _eventHandlerTypeCache.GetOrAdd(eventType, type =>
            typeof(IEventHandler<>).MakeGenericType(type));
    }

    /// <summary>
    /// Gets valid behaviors, filtering out nulls efficiently with preserved order
    /// </summary>
    private object[] GetValidBehaviors(Type behaviorType)
    {
        var behaviors = _serviceProvider.GetServices(behaviorType);
        
        // Use List for better performance than LINQ + ToArray
        // Preserve order from DI container registration
        var validBehaviors = new List<object>();
        foreach (var behavior in behaviors)
        {
            if (behavior != null)
                validBehaviors.Add(behavior);
        }
        
        return validBehaviors.ToArray();
    }

    /// <summary>
    /// Gets valid event handlers, filtering out nulls efficiently with preserved order
    /// </summary>
    private object[] GetValidEventHandlers(Type handlerType)
    {
        var handlers = _serviceProvider.GetServices(handlerType);
        
        // Use List for better performance than LINQ + ToArray
        // Preserve order from DI container registration
        var validHandlers = new List<object>();
        foreach (var handler in handlers)
        {
            if (handler != null)
                validHandlers.Add(handler);
        }
        
        return validHandlers.ToArray();
    }

    /// <summary>
    /// Builds the pipeline in correct FIFO order
    /// </summary>
    /// <remarks>
    /// Pipeline execution order: First registered behavior -> Last registered behavior -> Handler
    /// This ensures FIFO (First In, First Out) behavior execution order.
    /// 
    /// Example: If behaviors are registered as [A, B, C], execution will be A -> B -> C -> Handler
    /// </remarks>
    private static PipelineNext<TResponse> BuildPipeline<TResponse>(
        object handler, 
        object request, 
        object[] behaviors, 
        CancellationToken cancellationToken)
    {
        // Start with the base handler
        PipelineNext<TResponse> pipeline = () => 
            MediatorExecutionHelper.ExecuteHandler<TResponse>(handler, request, cancellationToken);

        // Build pipeline in reverse order to achieve FIFO execution
        // behaviors[0] should execute first, so it should be the outermost wrapper
        // behaviors[n-1] should execute last before handler, so it should be the innermost wrapper
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            pipeline = CreateBehaviorPipeline(behaviors[i], request, pipeline, cancellationToken);
        }

        return pipeline;
    }

    /// <summary>
    /// Creates a behavior pipeline step without closure capture issues
    /// </summary>
    private static PipelineNext<TResponse> CreateBehaviorPipeline<TResponse>(
        object behavior,
        object request,
        PipelineNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Capture the values in local variables to avoid closure issues
        var capturedBehavior = behavior;
        var capturedRequest = request;
        var capturedNext = next;
        var capturedToken = cancellationToken;

        return () => MediatorExecutionHelper.ExecuteBehavior(
            capturedBehavior, 
            capturedRequest, 
            capturedNext, 
            capturedToken);
    }

    /// <summary>
    /// Executes event handlers sequentially in registration order
    /// </summary>
    /// <remarks>
    /// Event handlers are executed in the order they were registered (FIFO).
    /// Sequential execution is used for predictable execution order and better resource management.
    /// </remarks>
    private async Task ExecuteEventHandlers<TEvent>(
        object[] handlers,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : class
    {
        // Execute handlers sequentially in registration order (FIFO)
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MediatorExecutionHelper.ExecuteEventHandler(handler, @event, cancellationToken);
        }
    }
}
