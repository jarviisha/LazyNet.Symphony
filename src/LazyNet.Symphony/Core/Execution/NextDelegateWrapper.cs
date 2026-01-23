using System.Reflection;

namespace LazyNet.Symphony.Core.Execution;

/// <summary>
/// Handles wrapping and conversion of pipeline next delegates.
/// </summary>
internal static class NextDelegateWrapper
{
    /// <summary>
    /// Wraps a typed PipelineNext delegate to an untyped version.
    /// </summary>
    public static PipelineNext<object> Wrap<TResponse>(PipelineNext<TResponse> next)
    {
        return async () =>
        {
            var result = await next().ConfigureAwait(false);
            return result!;
        };
    }

    /// <summary>
    /// Creates a typed next delegate from an untyped version.
    /// </summary>
    public static object CreateTypedNext(PipelineNext<object> next, Type expectedType)
    {
        if (IsPipelineNextType(expectedType))
        {
            return CreatePipelineNext(next, expectedType.GetGenericArguments()[0]);
        }

        if (IsFuncTaskType(expectedType))
        {
            var responseType = expectedType.GetGenericArguments()[0].GetGenericArguments()[0];
            return CreateFuncNext(next, responseType);
        }

        throw new ArgumentException($"Unsupported delegate type: {expectedType.Name}");
    }

    #region Type Checking

    private static bool IsPipelineNextType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PipelineNext<>);

    private static bool IsFuncTaskType(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(Func<>) &&
        type.GetGenericArguments()[0] is { IsGenericType: true } arg &&
        arg.GetGenericTypeDefinition() == typeof(Task<>);

    #endregion

    #region Delegate Creation

    private static object CreatePipelineNext(PipelineNext<object> next, Type responseType)
    {
        var method = typeof(NextDelegateWrapper)
            .GetMethod(nameof(CreatePipelineNextGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(responseType);

        return method.Invoke(null, [next])!;
    }

    private static object CreateFuncNext(PipelineNext<object> next, Type responseType)
    {
        var method = typeof(NextDelegateWrapper)
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
}
