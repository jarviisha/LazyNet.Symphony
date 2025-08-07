using System.Collections.Concurrent;
using System.Reflection;
using LazyNet.Symphony.Exceptions;

namespace LazyNet.Symphony.Core;

/// <summary>
/// Delegate for pipeline next function execution
/// </summary>
/// <typeparam name="TResponse">The response type</typeparam>
/// <returns>A task that represents the asynchronous operation containing the response</returns>
public delegate Task<TResponse> PipelineNext<TResponse>();

/// <summary>
/// Helper class for executing mediator operations with caching and performance optimizations
/// </summary>
internal static class MediatorExecutionHelper
{
    // Cache for compiled handler delegates to avoid reflection overhead
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task<object>>> HandlerCache = new();

    // Cache for compiled behavior delegates to avoid reflection overhead  
    private static readonly ConcurrentDictionary<Type, Func<object, object, PipelineNext<object>, CancellationToken, Task<object>>> BehaviorCache = new();

    // Cache for compiled event handler delegates to avoid reflection overhead
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task>> EventHandlerCache = new();

    /// <summary>
    /// Executes a request handler with caching
    /// </summary>
    public static async Task<TResponse> ExecuteHandler<TResponse>(object handler, object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var handlerType = handler.GetType();

        // Get or create cached compiled delegate
        var compiledHandler = HandlerCache.GetOrAdd(handlerType, type =>
        {
            var handleMethod = GetHandleMethod(type, request.GetType());
            return CreateHandlerDelegate(handleMethod);
        });

        var result = await compiledHandler(handler, request, cancellationToken);
        return (TResponse)result;
    }

    /// <summary>
    /// Executes a pipeline behavior with caching
    /// </summary>
    public static async Task<TResponse> ExecuteBehavior<TResponse>(object behavior, object request,
        PipelineNext<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var behaviorType = behavior.GetType();

        // Get or create cached compiled delegate
        var compiledBehavior = BehaviorCache.GetOrAdd(behaviorType, type =>
        {
            var handleMethod = GetBehaviorHandleMethod(type, request.GetType(), typeof(TResponse));
            return CreateBehaviorDelegate(handleMethod);
        });

        // Wrap the typed next delegate to return object
        PipelineNext<object> wrappedNext = async () =>
        {
            var result = await next();
            return (object?)result!;
        };

        var result = await compiledBehavior(behavior, request, wrappedNext, cancellationToken);
        return (TResponse)result;
    }

    /// <summary>
    /// Executes an event handler with caching
    /// </summary>
    public static async Task ExecuteEventHandler(object handler, object domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(domainEvent);

        var handlerType = handler.GetType();

        // Get or create cached compiled delegate
        var compiledHandler = EventHandlerCache.GetOrAdd(handlerType, type =>
        {
            var handleMethod = GetEventHandleMethod(type, domainEvent.GetType());
            return CreateEventHandlerDelegate(handleMethod);
        });

        await compiledHandler(handler, domainEvent, cancellationToken);
    }

    /// <summary>
    /// Gets the Handle method from a request handler type with specific signature matching
    /// </summary>
    private static MethodInfo GetHandleMethod(Type handlerType, Type requestType)
    {
        // First try to find the method directly on the type
        var methods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Handle")
            .Where(m => m.GetParameters().Length == 2)
            .Where(m => m.GetParameters()[0].ParameterType.IsAssignableFrom(requestType))
            .Where(m => m.GetParameters()[1].ParameterType == typeof(CancellationToken))
            .ToArray();

        if (methods.Length == 1)
            return methods[0];

        // Try to find Handle method from interfaces
        var interfaces = handlerType.GetInterfaces();
        foreach (var @interface in interfaces)
        {
            var interfaceMethods = @interface.GetMethods()
                .Where(m => m.Name == "Handle")
                .Where(m => m.GetParameters().Length == 2)
                .Where(m => m.GetParameters()[0].ParameterType.IsAssignableFrom(requestType))
                .Where(m => m.GetParameters()[1].ParameterType == typeof(CancellationToken))
                .ToArray();

            if (interfaceMethods.Length == 1)
                return interfaceMethods[0];
        }

        throw new MethodResolutionException(handlerType, "Handle", new[] { requestType, typeof(CancellationToken) })
            .WithContext("RequestType", requestType.FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", interfaces.Select(i => i.Name)));
    }

    /// <summary>
    /// Gets the Handle method from a behavior type with specific signature matching
    /// </summary>
    private static MethodInfo GetBehaviorHandleMethod(Type behaviorType, Type requestType, Type responseType)
    {
        var nextDelegateType = typeof(PipelineNext<>).MakeGenericType(responseType);

        // Try to find the method directly on the type
        var methods = behaviorType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Handle")
            .Where(m => m.GetParameters().Length == 3)
            .Where(m => m.GetParameters()[0].ParameterType.IsAssignableFrom(requestType))
            .Where(m => m.GetParameters()[1].ParameterType == nextDelegateType)
            .Where(m => m.GetParameters()[2].ParameterType == typeof(CancellationToken))
            .ToArray();

        if (methods.Length == 1)
            return methods[0];

        // Also try with Func<Task<T>> for backward compatibility
        var funcNextDelegateType = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseType));
        var funcMethods = behaviorType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Handle")
            .Where(m => m.GetParameters().Length == 3)
            .Where(m => m.GetParameters()[0].ParameterType.IsAssignableFrom(requestType))
            .Where(m => m.GetParameters()[1].ParameterType == funcNextDelegateType)
            .Where(m => m.GetParameters()[2].ParameterType == typeof(CancellationToken))
            .ToArray();

        if (funcMethods.Length == 1)
            return funcMethods[0];

        // Try to find Handle method from interfaces
        var interfaces = behaviorType.GetInterfaces();
        foreach (var @interface in interfaces)
        {
            var interfaceMethods = @interface.GetMethods()
                .Where(m => m.Name == "Handle")
                .Where(m => m.GetParameters().Length == 3)
                .Where(m => m.GetParameters()[0].ParameterType.IsAssignableFrom(requestType))
                .Where(m => m.GetParameters()[1].ParameterType == nextDelegateType || m.GetParameters()[1].ParameterType == funcNextDelegateType)
                .Where(m => m.GetParameters()[2].ParameterType == typeof(CancellationToken))
                .ToArray();

            if (interfaceMethods.Length == 1)
                return interfaceMethods[0];
        }

        throw new MethodResolutionException(behaviorType, "Handle", new[] { requestType, nextDelegateType, typeof(CancellationToken) })
            .WithContext("RequestType", requestType.FullName)
            .WithContext("ResponseType", responseType.FullName)
            .WithContext("NextDelegateType", nextDelegateType.FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", interfaces.Select(i => i.Name)));
    }

    /// <summary>
    /// Gets the Handle method from an event handler type with specific signature matching
    /// </summary>
    private static MethodInfo GetEventHandleMethod(Type handlerType, Type eventType)
    {
        // Try to find the method directly on the type
        var methods = handlerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Handle")
            .Where(m => m.GetParameters().Length == 2)
            .Where(m => m.GetParameters()[0].ParameterType.IsAssignableFrom(eventType))
            .Where(m => m.GetParameters()[1].ParameterType == typeof(CancellationToken))
            .Where(m => m.ReturnType == typeof(Task))
            .ToArray();

        if (methods.Length == 1)
            return methods[0];

        // Try to find Handle method from interfaces
        var interfaces = handlerType.GetInterfaces();
        foreach (var @interface in interfaces)
        {
            var interfaceMethods = @interface.GetMethods()
                .Where(m => m.Name == "Handle")
                .Where(m => m.GetParameters().Length == 2)
                .Where(m => m.GetParameters()[0].ParameterType.IsAssignableFrom(eventType))
                .Where(m => m.GetParameters()[1].ParameterType == typeof(CancellationToken))
                .Where(m => m.ReturnType == typeof(Task))
                .ToArray();

            if (interfaceMethods.Length == 1)
                return interfaceMethods[0];
        }

        throw new MethodResolutionException(handlerType, "Handle", new[] { eventType, typeof(CancellationToken) })
            .WithContext("EventType", eventType.FullName)
            .WithContext("ExpectedReturnType", typeof(Task).FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", interfaces.Select(i => i.Name)));
    }

    /// <summary>
    /// Creates a compiled delegate for request handlers
    /// </summary>
    private static Func<object, object, CancellationToken, Task<object>> CreateHandlerDelegate(MethodInfo handleMethod)
    {
        // Use direct reflection invoke to avoid complex Expression compilation issues
        return async (handler, request, token) =>
        {
            Task task;
            try
            {
                task = (Task?)handleMethod.Invoke(handler, new object[] { request, token })
                    ?? throw new InvalidOperationException("Handler returned null task");
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception to preserve the original stack trace
                throw ex.InnerException ?? ex;
            }

            try
            {
                await task;
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception to preserve the original stack trace
                throw ex.InnerException ?? ex;
            }

            // Get result from Task<T> using reflection
            var resultProperty = task.GetType().GetProperty("Result");
            if (resultProperty == null)
                throw new InvalidOperationException("Unable to get Result property from task");

            return resultProperty.GetValue(task) ?? throw new InvalidOperationException("Handler returned null result");
        };
    }

    /// <summary>
    /// Creates a compiled delegate for pipeline behaviors
    /// </summary>
    private static Func<object, object, PipelineNext<object>, CancellationToken, Task<object>> CreateBehaviorDelegate(MethodInfo handleMethod)
    {
        return async (behavior, request, next, token) =>
        {
            try
            {
                // Get the expected next delegate type from the behavior's Handle method
                var parameters = handleMethod.GetParameters();
                if (parameters.Length < 2)
                    throw new ArgumentException($"Behavior Handle method must have at least 2 parameters, got {parameters.Length}");

                var nextParameterType = parameters[1].ParameterType; // Should be PipelineNext<TResponse> or Func<Task<TResponse>>

                // Create a wrapper that matches the expected delegate type
                var nextDelegate = CreateTypedNextDelegate(next, nextParameterType);

                Task task;
                try
                {
                    task = (Task?)handleMethod.Invoke(behavior, new object[] { request, nextDelegate, token })
                        ?? throw new InvalidOperationException("Behavior returned null task");
                }
                catch (TargetInvocationException ex)
                {
                    // Unwrap the inner exception to preserve the original stack trace
                    throw ex.InnerException ?? ex;
                }

                try
                {
                    await task;
                }
                catch (TargetInvocationException ex)
                {
                    // Unwrap the inner exception to preserve the original stack trace
                    throw ex.InnerException ?? ex;
                }

                // Get result from Task<T>
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty == null)
                    throw new InvalidOperationException("Unable to get Result property from task");

                return resultProperty.GetValue(task) ?? throw new InvalidOperationException("Behavior returned null result");
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception to preserve the original stack trace
                throw ex.InnerException ?? ex;
            }
        };
    }

    /// <summary>
    /// Creates a typed next delegate that matches the expected signature (supports both PipelineNext and Func delegates)
    /// </summary>
    private static object CreateTypedNextDelegate(PipelineNext<object> next, Type expectedDelegateType)
    {
        // Check if it's a PipelineNext<T> delegate
        if (expectedDelegateType.IsGenericType && expectedDelegateType.GetGenericTypeDefinition() == typeof(PipelineNext<>))
        {
            var responseType = expectedDelegateType.GetGenericArguments()[0];
            
            // Create a method that wraps our PipelineNext<object> to return PipelineNext<TResponse>
            var method = typeof(MediatorExecutionHelper)
                .GetMethod(nameof(CreateTypedPipelineNextDelegate), BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
                throw new MethodResolutionException(typeof(MediatorExecutionHelper), nameof(CreateTypedPipelineNextDelegate));

            var genericMethod = method.MakeGenericMethod(responseType);
            var result = genericMethod.Invoke(null, new object[] { next });

            return result ?? throw new InvalidOperationException("Failed to create typed pipeline next delegate");
        }

        // Check if it's a Func<Task<T>> delegate (backward compatibility)
        if (expectedDelegateType.IsGenericType && expectedDelegateType.GetGenericTypeDefinition() == typeof(Func<>))
        {
            var genericArgs = expectedDelegateType.GetGenericArguments();
                if (genericArgs.Length != 1)
                    throw new ArgumentException($"Expected delegate type must have exactly one generic argument, got {genericArgs.Length}");

                var taskType = genericArgs[0]; // Task<TResponse>
                if (!taskType.IsGenericType || taskType.GetGenericTypeDefinition() != typeof(Task<>))
                    throw new ArgumentException($"Expected Task<T> type, got {taskType.Name}");

                var responseType = taskType.GetGenericArguments()[0]; // TResponse

                // Create a method that wraps our PipelineNext<object> to return Func<Task<TResponse>>
                var method = typeof(MediatorExecutionHelper)
                    .GetMethod(nameof(CreateTypedFuncDelegate), BindingFlags.NonPublic | BindingFlags.Static);

                if (method == null)
                    throw new MethodResolutionException(typeof(MediatorExecutionHelper), nameof(CreateTypedFuncDelegate));

                var genericMethod = method.MakeGenericMethod(responseType);
                var result = genericMethod.Invoke(null, new object[] { next });

                return result ?? throw new InvalidOperationException("Failed to create typed func delegate");
            }

            throw new ArgumentException($"Unsupported delegate type: {expectedDelegateType.Name}. Expected PipelineNext<T> or Func<Task<T>>");
    }

    /// <summary>
    /// Generic helper to create typed PipelineNext delegate
    /// </summary>
    private static PipelineNext<TResponse> CreateTypedPipelineNextDelegate<TResponse>(PipelineNext<object> next)
    {
        return async () =>
        {
            var result = await next();
            return (TResponse)result;
        };
    }

    /// <summary>
    /// Generic helper to create typed Func delegate (for backward compatibility)
    /// </summary>
    private static Func<Task<TResponse>> CreateTypedFuncDelegate<TResponse>(PipelineNext<object> next)
    {
        return async () =>
        {
            var result = await next();
            return (TResponse)result;
        };
    }

    /// <summary>
    /// Generic helper to create typed next delegate (deprecated - kept for compatibility)
    /// </summary>
    private static Func<Task<TResponse>> CreateTypedNextDelegateGeneric<TResponse>(Func<Task<object>> next)
    {
        return async () =>
        {
            var result = await next();
            return (TResponse)result;
        };
    }

    /// <summary>
    /// Creates a compiled delegate for event handlers
    /// </summary>
    private static Func<object, object, CancellationToken, Task> CreateEventHandlerDelegate(MethodInfo handleMethod)
    {
        // For event handlers, we'll use direct reflection invoke since they return Task (not Task<T>)
        return async (handler, domainEvent, token) =>
        {
            Task task;
            try
            {
                task = (Task?)handleMethod.Invoke(handler, new object[] { domainEvent, token })
                    ?? throw new InvalidOperationException("Event handler returned null task");
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception to preserve the original stack trace
                throw ex.InnerException ?? ex;
            }

            try
            {
                await task;
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception to preserve the original stack trace
                throw ex.InnerException ?? ex;
            }
        };
    }
}
