using System.Collections.Concurrent;
using System.Linq.Expressions;
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
/// Helper class for executing mediator operations with caching and performance optimizations.
/// Uses Expression Trees and compiled delegates for high-performance handler invocation.
/// </summary>
internal static class MediatorExecutionHelper
{
    #region Constants

    private const string HandleMethodName = "Handle";
    private const int RequestHandlerParameterCount = 2;
    private const int BehaviorParameterCount = 3;
    private const int EventHandlerParameterCount = 2;

    #endregion

    #region Delegate Caches

    // Unified cache for compiled handler delegates (includes method resolution)
    private static readonly ConcurrentDictionary<(Type HandlerType, Type RequestType), Func<object, object, CancellationToken, Task<object>>> HandlerDelegateCache = new();
    private static readonly ConcurrentDictionary<(Type BehaviorType, Type RequestType), Func<object, object, PipelineNext<object>, CancellationToken, Task<object>>> BehaviorDelegateCache = new();
    private static readonly ConcurrentDictionary<(Type HandlerType, Type EventType), Func<object, object, CancellationToken, Task>> EventHandlerDelegateCache = new();

    // Cache for Task<T>.Result property access (performance optimization)
    private static readonly ConcurrentDictionary<Type, PropertyInfo> TaskResultPropertyCache = new();

    #endregion

    #region Public API

    /// <summary>
    /// Executes a request handler with caching and compiled delegate optimization.
    /// </summary>
    public static async Task<TResponse> ExecuteHandler<TResponse>(object handler, object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var cacheKey = (handler.GetType(), request.GetType());
        var compiledHandler = HandlerDelegateCache.GetOrAdd(cacheKey, CreateHandlerDelegate);

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
        var compiledBehavior = BehaviorDelegateCache.GetOrAdd(cacheKey, key =>
            CreateBehaviorDelegate(key, typeof(TResponse)));

        var wrappedNext = WrapNextDelegate(next);
        var result = await compiledBehavior(behavior, request, wrappedNext, cancellationToken).ConfigureAwait(false);
        return (TResponse)result;
    }

    /// <summary>
    /// Executes an event handler with caching and compiled delegate optimization.
    /// </summary>
    public static async Task ExecuteEventHandler(object handler, object domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(domainEvent);

        var cacheKey = (handler.GetType(), domainEvent.GetType());
        var compiledHandler = EventHandlerDelegateCache.GetOrAdd(cacheKey, CreateEventHandlerDelegate);

        await compiledHandler(handler, domainEvent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all cached delegates and method info. Useful for testing and hot reload scenarios.
    /// </summary>
    public static void ClearCache()
    {
        HandlerDelegateCache.Clear();
        BehaviorDelegateCache.Clear();
        EventHandlerDelegateCache.Clear();
        TaskResultPropertyCache.Clear();
    }

    #endregion

    #region Delegate Factory Methods

    private static Func<object, object, CancellationToken, Task<object>> CreateHandlerDelegate(
        (Type HandlerType, Type RequestType) key)
    {
        var method = ResolveMethod(
            key.HandlerType,
            key.RequestType,
            RequestHandlerParameterCount,
            ValidateRequestHandlerMethod,
            () => CreateHandlerResolutionException(key.HandlerType, key.RequestType));

        return CompileHandlerDelegate(method);
    }

    private static Func<object, object, PipelineNext<object>, CancellationToken, Task<object>> CreateBehaviorDelegate(
        (Type BehaviorType, Type RequestType) key,
        Type responseType)
    {
        var nextDelegateType = typeof(PipelineNext<>).MakeGenericType(responseType);
        var funcNextDelegateType = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseType));

        var method = ResolveBehaviorMethod(
            key.BehaviorType,
            key.RequestType,
            nextDelegateType,
            funcNextDelegateType,
            () => CreateBehaviorResolutionException(key.BehaviorType, key.RequestType, responseType, nextDelegateType));

        return CompileBehaviorDelegate(method);
    }

    private static Func<object, object, CancellationToken, Task> CreateEventHandlerDelegate(
        (Type HandlerType, Type EventType) key)
    {
        var method = ResolveMethod(
            key.HandlerType,
            key.EventType,
            EventHandlerParameterCount,
            ValidateEventHandlerMethod,
            () => CreateEventResolutionException(key.HandlerType, key.EventType));

        return CompileEventHandlerDelegate(method);
    }

    #endregion

    #region Method Resolution

    private static MethodInfo ResolveMethod(
        Type handlerType,
        Type parameterType,
        int expectedParameterCount,
        Func<MethodInfo, Type, bool> validator,
        Func<MethodResolutionException> exceptionFactory)
    {
        // Try direct methods first (better performance)
        var method = handlerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == HandleMethodName)
            .Where(m => m.GetParameters().Length == expectedParameterCount)
            .SingleOrDefault(m => validator(m, parameterType));

        if (method != null) return method;

        // Fallback to interface methods
        method = handlerType
            .GetInterfaces()
            .SelectMany(i => i.GetMethods())
            .Where(m => m.Name == HandleMethodName)
            .Where(m => m.GetParameters().Length == expectedParameterCount)
            .SingleOrDefault(m => validator(m, parameterType));

        return method ?? throw exceptionFactory();
    }

    private static MethodInfo ResolveBehaviorMethod(
        Type behaviorType,
        Type requestType,
        Type nextDelegateType,
        Type funcNextDelegateType,
        Func<MethodResolutionException> exceptionFactory)
    {
        bool Validator(MethodInfo m) => ValidateBehaviorMethod(m, requestType, nextDelegateType, funcNextDelegateType);

        // Try direct methods first
        var method = behaviorType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == HandleMethodName)
            .Where(m => m.GetParameters().Length == BehaviorParameterCount)
            .SingleOrDefault(Validator);

        if (method != null) return method;

        // Fallback to interface methods
        method = behaviorType
            .GetInterfaces()
            .SelectMany(i => i.GetMethods())
            .Where(m => m.Name == HandleMethodName)
            .Where(m => m.GetParameters().Length == BehaviorParameterCount)
            .SingleOrDefault(Validator);

        return method ?? throw exceptionFactory();
    }

    private static bool ValidateRequestHandlerMethod(MethodInfo method, Type requestType)
    {
        var parameters = method.GetParameters();
        return parameters[0].ParameterType.IsAssignableFrom(requestType) &&
               parameters[1].ParameterType == typeof(CancellationToken);
    }

    private static bool ValidateBehaviorMethod(MethodInfo method, Type requestType, Type nextDelegateType, Type funcNextDelegateType)
    {
        var parameters = method.GetParameters();
        return parameters[0].ParameterType.IsAssignableFrom(requestType) &&
               (parameters[1].ParameterType == nextDelegateType || parameters[1].ParameterType == funcNextDelegateType) &&
               parameters[2].ParameterType == typeof(CancellationToken);
    }

    private static bool ValidateEventHandlerMethod(MethodInfo method, Type eventType)
    {
        var parameters = method.GetParameters();
        return parameters[0].ParameterType.IsAssignableFrom(eventType) &&
               parameters[1].ParameterType == typeof(CancellationToken) &&
               method.ReturnType == typeof(Task);
    }

    #endregion

    #region Expression Tree Compilation

    private static Func<object, object, CancellationToken, Task<object>> CompileHandlerDelegate(MethodInfo method)
    {
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var tokenParam = Expression.Parameter(typeof(CancellationToken), "token");

        var handlerType = method.DeclaringType!;
        var requestType = method.GetParameters()[0].ParameterType;

        var methodCall = Expression.Call(
            Expression.Convert(handlerParam, handlerType),
            method,
            Expression.Convert(requestParam, requestType),
            tokenParam);

        var awaitAndExtract = Expression.Call(
            typeof(MediatorExecutionHelper),
            nameof(AwaitAndExtractResult),
            Type.EmptyTypes,
            methodCall);

        var lambda = Expression.Lambda<Func<object, object, CancellationToken, Task<object>>>(
            awaitAndExtract, handlerParam, requestParam, tokenParam);

        var compiled = lambda.Compile();

        // Wrap with exception unwrapping
        return async (handler, request, token) =>
        {
            try
            {
                return await compiled(handler, request, token).ConfigureAwait(false);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };
    }

    private static Func<object, object, PipelineNext<object>, CancellationToken, Task<object>> CompileBehaviorDelegate(MethodInfo method)
    {
        // Behavior delegates use hybrid approach due to complex generic delegate handling
        return async (behavior, request, next, token) =>
        {
            try
            {
                var nextParameterType = method.GetParameters()[1].ParameterType;
                var typedNext = CreateTypedNextDelegate(next, nextParameterType);

                var taskResult = method.Invoke(behavior, [request, typedNext, token]);
                if (taskResult is not Task task)
                    throw new InvalidOperationException($"Method {method.Name} must return a Task");

                await task.ConfigureAwait(false);
                return ExtractTaskResult(task);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };
    }

    private static Func<object, object, CancellationToken, Task> CompileEventHandlerDelegate(MethodInfo method)
    {
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var eventParam = Expression.Parameter(typeof(object), "domainEvent");
        var tokenParam = Expression.Parameter(typeof(CancellationToken), "token");

        var handlerType = method.DeclaringType!;
        var eventType = method.GetParameters()[0].ParameterType;

        var methodCall = Expression.Call(
            Expression.Convert(handlerParam, handlerType),
            method,
            Expression.Convert(eventParam, eventType),
            tokenParam);

        var lambda = Expression.Lambda<Func<object, object, CancellationToken, Task>>(
            methodCall, handlerParam, eventParam, tokenParam);

        var compiled = lambda.Compile();

        return async (handler, domainEvent, token) =>
        {
            try
            {
                await compiled(handler, domainEvent, token).ConfigureAwait(false);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };
    }

    #endregion

    #region Task Result Extraction

    private static async Task<object> AwaitAndExtractResult(Task task)
    {
        await task.ConfigureAwait(false);
        return ExtractTaskResult(task);
    }

    private static object ExtractTaskResult(Task task)
    {
        var taskType = task.GetType();
        var resultProperty = TaskResultPropertyCache.GetOrAdd(taskType, t =>
            t.GetProperty("Result") ?? throw new InvalidOperationException($"Unable to get Result property from {t.Name}"));

        var result = resultProperty.GetValue(task);

        // Only throw for null reference types, not for default value types
        if (result is null && !resultProperty.PropertyType.IsValueType)
        {
            throw new InvalidOperationException("Handler returned null result");
        }

        return result!;
    }

    #endregion

    #region Next Delegate Wrapping

    private static PipelineNext<object> WrapNextDelegate<TResponse>(PipelineNext<TResponse> next)
    {
        return async () =>
        {
            var result = await next().ConfigureAwait(false);
            return result!;
        };
    }

    private static object CreateTypedNextDelegate(PipelineNext<object> next, Type expectedType)
    {
        if (IsPipelineNextType(expectedType))
        {
            return CreateTypedPipelineNext(next, expectedType.GetGenericArguments()[0]);
        }

        if (IsFuncTaskType(expectedType))
        {
            var responseType = expectedType.GetGenericArguments()[0].GetGenericArguments()[0];
            return CreateTypedFuncNext(next, responseType);
        }

        throw new ArgumentException($"Unsupported delegate type: {expectedType.Name}");
    }

    private static bool IsPipelineNextType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PipelineNext<>);

    private static bool IsFuncTaskType(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(Func<>) &&
        type.GetGenericArguments()[0] is { IsGenericType: true } arg &&
        arg.GetGenericTypeDefinition() == typeof(Task<>);

    private static object CreateTypedPipelineNext(PipelineNext<object> next, Type responseType)
    {
        var method = typeof(MediatorExecutionHelper)
            .GetMethod(nameof(CreatePipelineNextGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(responseType);

        return method.Invoke(null, [next])!;
    }

    private static object CreateTypedFuncNext(PipelineNext<object> next, Type responseType)
    {
        var method = typeof(MediatorExecutionHelper)
            .GetMethod(nameof(CreateFuncNextGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(responseType);

        return method.Invoke(null, [next])!;
    }

    private static PipelineNext<TResponse> CreatePipelineNextGeneric<TResponse>(PipelineNext<object> next)
    {
        return async () => (TResponse)await next().ConfigureAwait(false);
    }

    private static Func<Task<TResponse>> CreateFuncNextGeneric<TResponse>(PipelineNext<object> next)
    {
        return async () => (TResponse)await next().ConfigureAwait(false);
    }

    #endregion

    #region Exception Factories

    private static MethodResolutionException CreateHandlerResolutionException(Type handlerType, Type requestType)
    {
        return new MethodResolutionException(handlerType, HandleMethodName, [requestType, typeof(CancellationToken)])
            .WithContext("RequestType", requestType.FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", handlerType.GetInterfaces().Select(i => i.Name)))
            as MethodResolutionException ?? throw new InvalidOperationException();
    }

    private static MethodResolutionException CreateBehaviorResolutionException(
        Type behaviorType, Type requestType, Type responseType, Type nextDelegateType)
    {
        return new MethodResolutionException(behaviorType, HandleMethodName, [requestType, nextDelegateType, typeof(CancellationToken)])
            .WithContext("RequestType", requestType.FullName)
            .WithContext("ResponseType", responseType.FullName)
            .WithContext("NextDelegateType", nextDelegateType.FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", behaviorType.GetInterfaces().Select(i => i.Name)))
            as MethodResolutionException ?? throw new InvalidOperationException();
    }

    private static MethodResolutionException CreateEventResolutionException(Type handlerType, Type eventType)
    {
        return new MethodResolutionException(handlerType, HandleMethodName, [eventType, typeof(CancellationToken)])
            .WithContext("EventType", eventType.FullName)
            .WithContext("ExpectedReturnType", typeof(Task).FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", handlerType.GetInterfaces().Select(i => i.Name)))
            as MethodResolutionException ?? throw new InvalidOperationException();
    }

    #endregion
}
