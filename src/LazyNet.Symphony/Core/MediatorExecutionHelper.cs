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
/// Helper class for executing mediator operations with caching and performance optimizations
/// </summary>
internal static class MediatorExecutionHelper
{
    // Constants to avoid magic strings and numbers
    private const string HANDLE_METHOD_NAME = "Handle";
    private const int REQUEST_HANDLER_PARAMETER_COUNT = 2;
    private const int BEHAVIOR_PARAMETER_COUNT = 3;
    private const int EVENT_HANDLER_PARAMETER_COUNT = 2;

    // Cache for compiled handler delegates
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task<object>>> HandlerCache = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object, PipelineNext<object>, CancellationToken, Task<object>>> BehaviorCache = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task>> EventHandlerCache = new();

    // Cache for method resolution to avoid repeated reflection
    private static readonly ConcurrentDictionary<(Type HandlerType, Type RequestType), MethodInfo> MethodCache = new();
    private static readonly ConcurrentDictionary<(Type BehaviorType, Type RequestType, Type ResponseType), MethodInfo> BehaviorMethodCache = new();
    private static readonly ConcurrentDictionary<(Type HandlerType, Type EventType), MethodInfo> EventMethodCache = new();

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
            var handleMethod = GetCachedHandleMethod(type, request.GetType());
            return CreateOptimizedHandlerDelegate(handleMethod);
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
            var handleMethod = GetCachedBehaviorHandleMethod(type, request.GetType(), typeof(TResponse));
            return CreateOptimizedBehaviorDelegate(handleMethod);
        });

        // Wrap the typed next delegate
        var wrappedNext = CreateWrappedNext(next);
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
            var handleMethod = GetCachedEventHandleMethod(type, domainEvent.GetType());
            return CreateOptimizedEventHandlerDelegate(handleMethod);
        });

        await compiledHandler(handler, domainEvent, cancellationToken);
    }

    #region Method Resolution with Caching

    private static MethodInfo GetCachedHandleMethod(Type handlerType, Type requestType)
    {
        return MethodCache.GetOrAdd((handlerType, requestType), key =>
            ResolveHandleMethod(key.HandlerType, key.RequestType));
    }

    private static MethodInfo GetCachedBehaviorHandleMethod(Type behaviorType, Type requestType, Type responseType)
    {
        return BehaviorMethodCache.GetOrAdd((behaviorType, requestType, responseType), key =>
            ResolveBehaviorHandleMethod(key.BehaviorType, key.RequestType, key.ResponseType));
    }

    private static MethodInfo GetCachedEventHandleMethod(Type handlerType, Type eventType)
    {
        return EventMethodCache.GetOrAdd((handlerType, eventType), key =>
            ResolveEventHandleMethod(key.HandlerType, key.EventType));
    }

    private static MethodInfo ResolveHandleMethod(Type handlerType, Type requestType)
    {
        // Try direct methods first
        var method = FindMethodOnType(handlerType, HANDLE_METHOD_NAME, 
            m => ValidateHandlerMethod(m, requestType));

        if (method != null) return method;

        // Try interface methods
        method = FindMethodOnInterfaces(handlerType, HANDLE_METHOD_NAME,
            m => ValidateHandlerMethod(m, requestType));

        if (method != null) return method;

        throw CreateMethodResolutionException(handlerType, requestType, typeof(CancellationToken));
    }

    private static MethodInfo ResolveBehaviorHandleMethod(Type behaviorType, Type requestType, Type responseType)
    {
        var nextDelegateType = typeof(PipelineNext<>).MakeGenericType(responseType);
        var funcNextDelegateType = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseType));

        // Try direct methods first
        var method = FindMethodOnType(behaviorType, HANDLE_METHOD_NAME,
            m => ValidateBehaviorMethod(m, requestType, nextDelegateType, funcNextDelegateType));

        if (method != null) return method;

        // Try interface methods
        method = FindMethodOnInterfaces(behaviorType, HANDLE_METHOD_NAME,
            m => ValidateBehaviorMethod(m, requestType, nextDelegateType, funcNextDelegateType));

        if (method != null) return method;

        throw CreateBehaviorMethodResolutionException(behaviorType, requestType, responseType, nextDelegateType);
    }

    private static MethodInfo ResolveEventHandleMethod(Type handlerType, Type eventType)
    {
        // Try direct methods first
        var method = FindMethodOnType(handlerType, HANDLE_METHOD_NAME,
            m => ValidateEventHandlerMethod(m, eventType));

        if (method != null) return method;

        // Try interface methods
        method = FindMethodOnInterfaces(handlerType, HANDLE_METHOD_NAME,
            m => ValidateEventHandlerMethod(m, eventType));

        if (method != null) return method;

        throw CreateEventMethodResolutionException(handlerType, eventType);
    }

    #endregion

    #region Method Finding Helpers

    private static MethodInfo? FindMethodOnType(Type type, string methodName, Func<MethodInfo, bool> validator)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .Where(validator)
            .SingleOrDefault();
    }

    private static MethodInfo? FindMethodOnInterfaces(Type type, string methodName, Func<MethodInfo, bool> validator)
    {
        return type.GetInterfaces()
            .SelectMany(i => i.GetMethods())
            .Where(m => m.Name == methodName)
            .Where(validator)
            .SingleOrDefault();
    }

    private static bool ValidateHandlerMethod(MethodInfo method, Type requestType)
    {
        var parameters = method.GetParameters();
        return parameters.Length == REQUEST_HANDLER_PARAMETER_COUNT &&
               parameters[0].ParameterType.IsAssignableFrom(requestType) &&
               parameters[1].ParameterType == typeof(CancellationToken);
    }

    private static bool ValidateBehaviorMethod(MethodInfo method, Type requestType, Type nextDelegateType, Type funcNextDelegateType)
    {
        var parameters = method.GetParameters();
        return parameters.Length == BEHAVIOR_PARAMETER_COUNT &&
               parameters[0].ParameterType.IsAssignableFrom(requestType) &&
               (parameters[1].ParameterType == nextDelegateType || parameters[1].ParameterType == funcNextDelegateType) &&
               parameters[2].ParameterType == typeof(CancellationToken);
    }

    private static bool ValidateEventHandlerMethod(MethodInfo method, Type eventType)
    {
        var parameters = method.GetParameters();
        return parameters.Length == EVENT_HANDLER_PARAMETER_COUNT &&
               parameters[0].ParameterType.IsAssignableFrom(eventType) &&
               parameters[1].ParameterType == typeof(CancellationToken) &&
               method.ReturnType == typeof(Task);
    }

    #endregion

    #region Optimized Delegate Creation with Expression Trees

    private static Func<object, object, CancellationToken, Task<object>> CreateOptimizedHandlerDelegate(MethodInfo handleMethod)
    {
        // Use compiled expressions for better performance than reflection
        var handlerParam = Expression.Parameter(typeof(object), "handler");
        var requestParam = Expression.Parameter(typeof(object), "request");
        var tokenParam = Expression.Parameter(typeof(CancellationToken), "token");

        var handlerType = handleMethod.DeclaringType ?? throw new InvalidOperationException("Method must have a declaring type");
        var requestType = handleMethod.GetParameters()[0].ParameterType;

        var handlerCast = Expression.Convert(handlerParam, handlerType);
        var requestCast = Expression.Convert(requestParam, requestType);

        var methodCall = Expression.Call(handlerCast, handleMethod, requestCast, tokenParam);

        // Handle Task<T> result extraction
        var taskResultExpr = CreateTaskResultExpression(methodCall);
        var lambda = Expression.Lambda<Func<object, object, CancellationToken, Task<object>>>(
            taskResultExpr, handlerParam, requestParam, tokenParam);

        var compiled = lambda.Compile();

        // Wrap with exception handling
        return async (handler, request, token) =>
        {
            try
            {
                return await compiled(handler, request, token);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };
    }

    private static Func<object, object, PipelineNext<object>, CancellationToken, Task<object>> CreateOptimizedBehaviorDelegate(MethodInfo handleMethod)
    {
        // For behaviors, we'll use a hybrid approach due to the complexity of generic delegate handling
        return async (behavior, request, next, token) =>
        {
            try
            {
                var parameters = handleMethod.GetParameters();
                var nextParameterType = parameters[1].ParameterType;
                var nextDelegate = CreateTypedNextDelegate(next, nextParameterType);

                var task = await InvokeMethodSafely(handleMethod, behavior, new object[] { request, nextDelegate, token });
                return GetTaskResult(task);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };
    }

    private static Func<object, object, CancellationToken, Task> CreateOptimizedEventHandlerDelegate(MethodInfo handleMethod)
    {
        return async (handler, domainEvent, token) =>
        {
            try
            {
                var task = await InvokeMethodSafely(handleMethod, handler, new object[] { domainEvent, token });
                await task;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        };
    }

    #endregion

    #region Common Utilities

    private static PipelineNext<object> CreateWrappedNext<TResponse>(PipelineNext<TResponse> next)
    {
        return async () =>
        {
            var result = await next();
            return (object?)result!;
        };
    }

    private static async Task<Task> InvokeMethodSafely(MethodInfo method, object instance, object[] parameters)
    {
        var result = method.Invoke(instance, parameters);
        if (result is not Task task)
            throw new InvalidOperationException($"Method {method.Name} must return a Task");

        await task;
        return task;
    }

    private static object GetTaskResult(Task task)
    {
        var resultProperty = task.GetType().GetProperty("Result")
            ?? throw new InvalidOperationException("Unable to get Result property from task");

        return resultProperty.GetValue(task) 
            ?? throw new InvalidOperationException("Method returned null result");
    }

    private static Expression CreateTaskResultExpression(Expression taskExpression)
    {
        // Create an expression that handles async Task<T> result extraction
        var awaitMethod = typeof(MediatorExecutionHelper).GetMethod(nameof(AwaitTaskAndGetResult), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AwaitTaskAndGetResult method not found");

        return Expression.Call(awaitMethod, taskExpression);
    }

    private static async Task<object> AwaitTaskAndGetResult(Task task)
    {
        await task;
        return GetTaskResult(task);
    }

    private static object CreateTypedNextDelegate(PipelineNext<object> next, Type expectedDelegateType)
    {
        if (IsPipelineNextType(expectedDelegateType))
        {
            return CreatePipelineNextDelegate(next, expectedDelegateType);
        }

        if (IsFuncTaskType(expectedDelegateType))
        {
            return CreateFuncTaskDelegate(next, expectedDelegateType);
        }

        throw new ArgumentException($"Unsupported delegate type: {expectedDelegateType.Name}. Expected PipelineNext<T> or Func<Task<T>>");
    }

    private static bool IsPipelineNextType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PipelineNext<>);

    private static bool IsFuncTaskType(Type type) =>
        type.IsGenericType && 
        type.GetGenericTypeDefinition() == typeof(Func<>) &&
        type.GetGenericArguments()[0].IsGenericType &&
        type.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Task<>);

    private static object CreatePipelineNextDelegate(PipelineNext<object> next, Type delegateType)
    {
        var responseType = delegateType.GetGenericArguments()[0];
        var method = typeof(MediatorExecutionHelper)
            .GetMethod(nameof(CreateTypedPipelineNextDelegate), BindingFlags.NonPublic | BindingFlags.Static)
            ?.MakeGenericMethod(responseType);

        return method?.Invoke(null, new object[] { next })
            ?? throw new InvalidOperationException("Failed to create typed pipeline next delegate");
    }

    private static object CreateFuncTaskDelegate(PipelineNext<object> next, Type delegateType)
    {
        var responseType = delegateType.GetGenericArguments()[0].GetGenericArguments()[0];
        var method = typeof(MediatorExecutionHelper)
            .GetMethod(nameof(CreateTypedFuncDelegate), BindingFlags.NonPublic | BindingFlags.Static)
            ?.MakeGenericMethod(responseType);

        return method?.Invoke(null, new object[] { next })
            ?? throw new InvalidOperationException("Failed to create typed func delegate");
    }

    private static PipelineNext<TResponse> CreateTypedPipelineNextDelegate<TResponse>(PipelineNext<object> next)
    {
        return async () =>
        {
            var result = await next();
            return (TResponse)result;
        };
    }

    private static Func<Task<TResponse>> CreateTypedFuncDelegate<TResponse>(PipelineNext<object> next)
    {
        return async () =>
        {
            var result = await next();
            return (TResponse)result;
        };
    }

    #endregion

    #region Exception Creation Helpers

    private static MethodResolutionException CreateMethodResolutionException(Type handlerType, Type requestType, Type cancellationTokenType)
    {
        var exception = new MethodResolutionException(handlerType, HANDLE_METHOD_NAME, new[] { requestType, cancellationTokenType });
        exception.WithContext("RequestType", requestType.FullName);
        exception.WithContext("SearchedInterfaces", string.Join(", ", handlerType.GetInterfaces().Select(i => i.Name)));
        return exception;
    }

    private static MethodResolutionException CreateBehaviorMethodResolutionException(Type behaviorType, Type requestType, Type responseType, Type nextDelegateType)
    {
        var exception = new MethodResolutionException(behaviorType, HANDLE_METHOD_NAME, new[] { requestType, nextDelegateType, typeof(CancellationToken) });
        exception.WithContext("RequestType", requestType.FullName);
        exception.WithContext("ResponseType", responseType.FullName);
        exception.WithContext("NextDelegateType", nextDelegateType.FullName);
        exception.WithContext("SearchedInterfaces", string.Join(", ", behaviorType.GetInterfaces().Select(i => i.Name)));
        return exception;
    }

    private static MethodResolutionException CreateEventMethodResolutionException(Type handlerType, Type eventType)
    {
        var exception = new MethodResolutionException(handlerType, HANDLE_METHOD_NAME, new[] { eventType, typeof(CancellationToken) });
        exception.WithContext("EventType", eventType.FullName);
        exception.WithContext("ExpectedReturnType", typeof(Task).FullName);
        exception.WithContext("SearchedInterfaces", string.Join(", ", handlerType.GetInterfaces().Select(i => i.Name)));
        return exception;
    }

    #endregion
}