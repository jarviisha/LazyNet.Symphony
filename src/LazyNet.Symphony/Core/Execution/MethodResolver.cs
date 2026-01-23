using System.Reflection;
using LazyNet.Symphony.Exceptions;

namespace LazyNet.Symphony.Core.Execution;

/// <summary>
/// Resolves handler methods using reflection with validation.
/// </summary>
internal static class MethodResolver
{
    private const string HandleMethodName = "Handle";
    private const int RequestHandlerParameterCount = 2;
    private const int BehaviorParameterCount = 3;
    private const int EventHandlerParameterCount = 2;

    /// <summary>
    /// Resolves the Handle method for a request handler.
    /// </summary>
    public static MethodInfo ResolveRequestHandlerMethod(Type handlerType, Type requestType)
    {
        var method = FindMethod(
            handlerType,
            RequestHandlerParameterCount,
            m => ValidateRequestHandlerMethod(m, requestType));

        return method ?? throw CreateHandlerException(handlerType, requestType);
    }

    /// <summary>
    /// Resolves the Handle method for a pipeline behavior.
    /// </summary>
    public static MethodInfo ResolveBehaviorMethod(
        Type behaviorType,
        Type requestType,
        Type responseType)
    {
        var nextDelegateType = typeof(PipelineNext<>).MakeGenericType(responseType);
        var funcNextDelegateType = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(responseType));

        var method = FindMethod(
            behaviorType,
            BehaviorParameterCount,
            m => ValidateBehaviorMethod(m, requestType, nextDelegateType, funcNextDelegateType));

        return method ?? throw CreateBehaviorException(behaviorType, requestType, responseType, nextDelegateType);
    }

    /// <summary>
    /// Resolves the Handle method for an event handler.
    /// </summary>
    public static MethodInfo ResolveEventHandlerMethod(Type handlerType, Type eventType)
    {
        var method = FindMethod(
            handlerType,
            EventHandlerParameterCount,
            m => ValidateEventHandlerMethod(m, eventType));

        return method ?? throw CreateEventException(handlerType, eventType);
    }

    #region Method Finding

    private static MethodInfo? FindMethod(Type type, int parameterCount, Func<MethodInfo, bool> validator)
    {
        // Try direct methods first (better performance)
        var method = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == HandleMethodName)
            .Where(m => m.GetParameters().Length == parameterCount)
            .SingleOrDefault(validator);

        if (method != null) return method;

        // Fallback to interface methods
        return type
            .GetInterfaces()
            .SelectMany(i => i.GetMethods())
            .Where(m => m.Name == HandleMethodName)
            .Where(m => m.GetParameters().Length == parameterCount)
            .SingleOrDefault(validator);
    }

    #endregion

    #region Validation

    private static bool ValidateRequestHandlerMethod(MethodInfo method, Type requestType)
    {
        var parameters = method.GetParameters();
        return parameters[0].ParameterType.IsAssignableFrom(requestType) &&
               parameters[1].ParameterType == typeof(CancellationToken);
    }

    private static bool ValidateBehaviorMethod(
        MethodInfo method,
        Type requestType,
        Type nextDelegateType,
        Type funcNextDelegateType)
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

    #region Exception Factories

    private static MethodResolutionException CreateHandlerException(Type handlerType, Type requestType)
    {
        return new MethodResolutionException(handlerType, HandleMethodName, [requestType, typeof(CancellationToken)])
            .WithContext("RequestType", requestType.FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", handlerType.GetInterfaces().Select(i => i.Name)))
            as MethodResolutionException ?? throw new InvalidOperationException();
    }

    private static MethodResolutionException CreateBehaviorException(
        Type behaviorType,
        Type requestType,
        Type responseType,
        Type nextDelegateType)
    {
        return new MethodResolutionException(behaviorType, HandleMethodName, [requestType, nextDelegateType, typeof(CancellationToken)])
            .WithContext("RequestType", requestType.FullName)
            .WithContext("ResponseType", responseType.FullName)
            .WithContext("NextDelegateType", nextDelegateType.FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", behaviorType.GetInterfaces().Select(i => i.Name)))
            as MethodResolutionException ?? throw new InvalidOperationException();
    }

    private static MethodResolutionException CreateEventException(Type handlerType, Type eventType)
    {
        return new MethodResolutionException(handlerType, HandleMethodName, [eventType, typeof(CancellationToken)])
            .WithContext("EventType", eventType.FullName)
            .WithContext("ExpectedReturnType", typeof(Task).FullName)
            .WithContext("SearchedInterfaces", string.Join(", ", handlerType.GetInterfaces().Select(i => i.Name)))
            as MethodResolutionException ?? throw new InvalidOperationException();
    }

    #endregion
}
