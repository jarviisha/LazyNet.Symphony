using System.Linq.Expressions;
using System.Reflection;

namespace LazyNet.Symphony.Core.Execution;

/// <summary>
/// Compiles delegates using Expression Trees for high-performance handler invocation.
/// </summary>
internal static class DelegateCompiler
{
    /// <summary>
    /// Compiles a request handler delegate using Expression Trees.
    /// </summary>
    public static Func<object, object, CancellationToken, Task<object>> CompileHandlerDelegate(MethodInfo method)
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
            typeof(DelegateCompiler),
            nameof(AwaitAndExtractResult),
            Type.EmptyTypes,
            methodCall);

        var lambda = Expression.Lambda<Func<object, object, CancellationToken, Task<object>>>(
            awaitAndExtract, handlerParam, requestParam, tokenParam);

        var compiled = lambda.Compile();

        return WrapWithExceptionHandling(compiled);
    }

    /// <summary>
    /// Compiles a pipeline behavior delegate.
    /// Uses hybrid approach due to complex generic delegate handling.
    /// </summary>
    public static Func<object, object, PipelineNext<object>, CancellationToken, Task<object>> CompileBehaviorDelegate(MethodInfo method)
    {
        return async (behavior, request, next, token) =>
        {
            try
            {
                var nextParameterType = method.GetParameters()[1].ParameterType;
                var typedNext = NextDelegateWrapper.CreateTypedNext(next, nextParameterType);

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

    /// <summary>
    /// Compiles an event handler delegate using Expression Trees.
    /// </summary>
    public static Func<object, object, CancellationToken, Task> CompileEventHandlerDelegate(MethodInfo method)
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

        return WrapEventWithExceptionHandling(compiled);
    }

    #region Task Result Extraction

    private static async Task<object> AwaitAndExtractResult(Task task)
    {
        await task.ConfigureAwait(false);
        return ExtractTaskResult(task);
    }

    internal static object ExtractTaskResult(Task task)
    {
        var taskType = task.GetType();
        var resultProperty = DelegateCache.TaskResultProperties.GetOrAdd(taskType, t =>
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

    #region Exception Wrapping

    private static Func<object, object, CancellationToken, Task<object>> WrapWithExceptionHandling(
        Func<object, object, CancellationToken, Task<object>> compiled)
    {
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

    private static Func<object, object, CancellationToken, Task> WrapEventWithExceptionHandling(
        Func<object, object, CancellationToken, Task> compiled)
    {
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
}
