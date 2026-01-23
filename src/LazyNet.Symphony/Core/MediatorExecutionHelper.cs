using LazyNet.Symphony.Core.Execution;

namespace LazyNet.Symphony.Core;

/// <summary>
/// Delegate for pipeline next function execution.
/// </summary>
/// <typeparam name="TResponse">The response type</typeparam>
/// <returns>A task that represents the asynchronous operation containing the response</returns>
public delegate Task<TResponse> PipelineNext<TResponse>();

/// <summary>
/// Helper class for executing mediator operations with caching and performance optimizations.
/// Uses Expression Trees and compiled delegates for high-performance handler invocation.
/// </summary>
internal static class MediatorExecutionHelper
{
    /// <summary>
    /// Executes a request handler with caching and compiled delegate optimization.
    /// </summary>
    public static async Task<TResponse> ExecuteHandler<TResponse>(
        object handler,
        object request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var cacheKey = (handler.GetType(), request.GetType());
        var compiledHandler = DelegateCache.HandlerDelegates.GetOrAdd(cacheKey, CreateHandlerDelegate);

        var result = await compiledHandler(handler, request, cancellationToken).ConfigureAwait(false);
        return (TResponse)result;
    }

    /// <summary>
    /// Executes a pipeline behavior with caching.
    /// </summary>
    public static async Task<TResponse> ExecuteBehavior<TResponse>(
        object behavior,
        object request,
        PipelineNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var cacheKey = (behavior.GetType(), request.GetType());
        var compiledBehavior = DelegateCache.BehaviorDelegates.GetOrAdd(cacheKey, key =>
            CreateBehaviorDelegate(key, typeof(TResponse)));

        var wrappedNext = NextDelegateWrapper.Wrap(next);
        var result = await compiledBehavior(behavior, request, wrappedNext, cancellationToken).ConfigureAwait(false);
        return (TResponse)result;
    }

    /// <summary>
    /// Executes an event handler with caching and compiled delegate optimization.
    /// </summary>
    public static async Task ExecuteEventHandler(
        object handler,
        object domainEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(domainEvent);

        var cacheKey = (handler.GetType(), domainEvent.GetType());
        var compiledHandler = DelegateCache.EventHandlerDelegates.GetOrAdd(cacheKey, CreateEventHandlerDelegate);

        await compiledHandler(handler, domainEvent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all cached delegates. Useful for testing and hot reload scenarios.
    /// </summary>
    public static void ClearCache() => DelegateCache.Clear();

    #region Delegate Factory Methods

    private static Func<object, object, CancellationToken, Task<object>> CreateHandlerDelegate(
        (Type HandlerType, Type RequestType) key)
    {
        var method = MethodResolver.ResolveRequestHandlerMethod(key.HandlerType, key.RequestType);
        return DelegateCompiler.CompileHandlerDelegate(method);
    }

    private static Func<object, object, PipelineNext<object>, CancellationToken, Task<object>> CreateBehaviorDelegate(
        (Type BehaviorType, Type RequestType) key,
        Type responseType)
    {
        var method = MethodResolver.ResolveBehaviorMethod(key.BehaviorType, key.RequestType, responseType);
        return DelegateCompiler.CompileBehaviorDelegate(method);
    }

    private static Func<object, object, CancellationToken, Task> CreateEventHandlerDelegate(
        (Type HandlerType, Type EventType) key)
    {
        var method = MethodResolver.ResolveEventHandlerMethod(key.HandlerType, key.EventType);
        return DelegateCompiler.CompileEventHandlerDelegate(method);
    }

    #endregion
}
