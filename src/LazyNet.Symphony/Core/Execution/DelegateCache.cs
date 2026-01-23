using System.Collections.Concurrent;
using System.Reflection;

namespace LazyNet.Symphony.Core.Execution;

/// <summary>
/// Manages caching of compiled delegates for handler execution.
/// Provides thread-safe access to cached delegates and cache clearing functionality.
/// </summary>
internal static class DelegateCache
{
    // Handler delegate caches
    internal static readonly ConcurrentDictionary<(Type HandlerType, Type RequestType), Func<object, object, CancellationToken, Task<object>>> HandlerDelegates = new();
    internal static readonly ConcurrentDictionary<(Type BehaviorType, Type RequestType), Func<object, object, PipelineNext<object>, CancellationToken, Task<object>>> BehaviorDelegates = new();
    internal static readonly ConcurrentDictionary<(Type HandlerType, Type EventType), Func<object, object, CancellationToken, Task>> EventHandlerDelegates = new();

    // Property cache for Task<T>.Result access
    internal static readonly ConcurrentDictionary<Type, PropertyInfo> TaskResultProperties = new();

    /// <summary>
    /// Clears all cached delegates. Useful for testing and hot reload scenarios.
    /// </summary>
    public static void Clear()
    {
        HandlerDelegates.Clear();
        BehaviorDelegates.Clear();
        EventHandlerDelegates.Clear();
        TaskResultProperties.Clear();
    }
}
