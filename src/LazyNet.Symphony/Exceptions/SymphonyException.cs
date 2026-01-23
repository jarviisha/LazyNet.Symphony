using System.Collections.Concurrent;
using System.Text;

namespace LazyNet.Symphony.Exceptions;

/// <summary>
/// Base exception class for all LazyNet Symphony mediator-related errors.
/// Provides a structured approach to exception handling with error codes and contextual information.
/// </summary>
/// <remarks>
/// <para>
/// This abstract base class serves as the foundation for all Symphony-specific exceptions,
/// providing consistent error reporting and diagnostic capabilities across the mediator framework.
/// </para>
/// <para>
/// Key features include:
/// <list type="bullet">
/// <item><description>Unique error codes for categorizing different types of failures</description></item>
/// <item><description>Context dictionary for storing additional diagnostic information</description></item>
/// <item><description>Fluent API for adding context data during exception construction</description></item>
/// <item><description>Enhanced ToString() formatting for better debugging experience</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Custom exception inheriting from SymphonyException
/// public class CustomException : SymphonyException
/// {
///     public override string ErrorCode => "CUSTOM_ERROR";
///     
///     public CustomException(string message) : base(message)
///     {
///         WithContext("Timestamp", DateTime.UtcNow)
///             .WithContext("Operation", "CustomOperation");
///     }
/// }
/// </code>
/// </example>
public abstract class SymphonyException : Exception
{
    private readonly ConcurrentDictionary<string, object?> _context = new();
    private volatile bool _isSealed;

    /// <summary>
    /// Gets the error code associated with this exception
    /// </summary>
    public abstract string ErrorCode { get; }

    /// <summary>
    /// Gets additional context data for this exception (thread-safe, read-only view)
    /// </summary>
    public IReadOnlyDictionary<string, object?> Context => _context;

    /// <summary>
    /// Gets a hint on how to resolve this exception (optional)
    /// </summary>
    public virtual string? ResolutionHint => null;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymphonyException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    protected SymphonyException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SymphonyException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    protected SymphonyException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Adds context data to the exception
    /// </summary>
    /// <param name="key">The context key (cannot be null or whitespace)</param>
    /// <param name="value">The context value</param>
    /// <returns>This exception instance for fluent chaining</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or whitespace</exception>
    /// <exception cref="InvalidOperationException">Thrown when exception context is sealed</exception>
    public SymphonyException WithContext(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_isSealed)
            throw new InvalidOperationException("Cannot modify context after exception is sealed");

        _context[key] = value;
        return this;
    }

    /// <summary>
    /// Adds context data to the exception with type safety (avoids boxing for value types when possible)
    /// </summary>
    /// <typeparam name="T">The type of the context value</typeparam>
    /// <param name="key">The context key (cannot be null or whitespace)</param>
    /// <param name="value">The context value</param>
    /// <returns>This exception instance for fluent chaining</returns>
    /// <exception cref="ArgumentException">Thrown when key is null or whitespace</exception>
    /// <exception cref="InvalidOperationException">Thrown when exception context is sealed</exception>
    public SymphonyException WithContext<T>(string key, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_isSealed)
            throw new InvalidOperationException("Cannot modify context after exception is sealed");

        _context[key] = value;
        return this;
    }

    /// <summary>
    /// Helper method to add type information to context (skips null types)
    /// </summary>
    /// <param name="key">The context key</param>
    /// <param name="type">The type to add (null values are skipped)</param>
    /// <returns>This exception instance for fluent chaining</returns>
    protected SymphonyException AddTypedContext(string key, Type? type)
    {
        if (type != null)
        {
            WithContext(key, type.FullName);
        }
        return this;
    }

    /// <summary>
    /// Seals the exception context, preventing further modifications.
    /// This is called internally when the exception is thrown.
    /// </summary>
    internal void Seal() => _isSealed = true;

    /// <summary>
    /// Exports exception data in a structured format suitable for logging frameworks
    /// </summary>
    /// <returns>Dictionary containing error code, message, stack trace, and context</returns>
    public IDictionary<string, object?> ToStructuredLog()
    {
        return new Dictionary<string, object?>
        {
            ["ErrorCode"] = ErrorCode,
            ["Message"] = Message,
            ["StackTrace"] = StackTrace,
            ["Context"] = new Dictionary<string, object?>(_context),
            ["ResolutionHint"] = ResolutionHint
        };
    }

    /// <summary>
    /// Gets a formatted error message including error code, context, and resolution hint
    /// </summary>
    /// <returns>Formatted error message</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append('[');
        builder.Append(ErrorCode);
        builder.Append("] ");
        builder.Append(base.ToString());

        if (_context.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Context: ");
            builder.AppendJoin(", ", _context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        if (!string.IsNullOrWhiteSpace(ResolutionHint))
        {
            builder.AppendLine();
            builder.Append("Resolution: ");
            builder.Append(ResolutionHint);
        }

        return builder.ToString();
    }
}

/// <summary>
/// Exception thrown when no handler is registered for a specific request type.
/// This typically occurs when attempting to send a request through the mediator
/// without having registered an appropriate handler in the DI container.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown by the mediator when it cannot locate a handler for the
/// specified request type during the Send operation. This usually indicates a configuration
/// issue where the handler was not properly registered during application startup.
/// </para>
/// <para>
/// The exception includes contextual information about the request type and expected handler
/// type to facilitate debugging and troubleshooting.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // This will throw HandlerNotFoundException if GetUserHandler is not registered
/// var request = new GetUserRequest { Id = 123 };
/// var result = await mediator.Send(request);
/// </code>
/// </example>
public sealed class HandlerNotFoundException : SymphonyException
{
    /// <summary>
    /// Gets the unique error code for handler not found exceptions.
    /// </summary>
    /// <value>Always returns "SYMPHONY_HANDLER_NOT_FOUND".</value>
    public override string ErrorCode => "SYMPHONY_HANDLER_NOT_FOUND";

    /// <summary>
    /// Gets the request type that has no registered handler
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// Gets the expected handler type
    /// </summary>
    public Type? ExpectedHandlerType { get; }

    /// <summary>
    /// Gets a hint on how to resolve this exception
    /// </summary>
    public override string ResolutionHint =>
        ExpectedHandlerType != null
            ? $"Register the handler using: services.AddRequestHandler<{RequestType.Name}, {ExpectedHandlerType.Name}>()"
            : $"Ensure that a handler for '{RequestType.Name}' is registered in the DI container using services.AddMediator()";

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerNotFoundException"/> class.
    /// </summary>
    /// <param name="requestType">The request type that has no registered handler.</param>
    /// <param name="expectedHandlerType">The expected handler type that should handle this request (optional).</param>
    /// <remarks>
    /// This constructor automatically populates the exception context with the request type
    /// and expected handler type information for debugging purposes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when requestType is null</exception>
    public HandlerNotFoundException(Type requestType, Type? expectedHandlerType = null)
        : base($"No handler registered for request type '{requestType?.Name ?? "Unknown"}'")
    {
        ArgumentNullException.ThrowIfNull(requestType);

        RequestType = requestType;
        ExpectedHandlerType = expectedHandlerType;

        WithContext("RequestType", requestType.FullName);
        AddTypedContext("ExpectedHandlerType", expectedHandlerType);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="requestType">The request type that has no registered handler.</param>
    /// <param name="expectedHandlerType">The expected handler type that should handle this request.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <remarks>
    /// This constructor automatically populates the exception context with the request type
    /// and expected handler type information for debugging purposes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when requestType is null</exception>
    public HandlerNotFoundException(Type requestType, Type? expectedHandlerType, Exception innerException)
        : base($"No handler registered for request type '{requestType?.Name ?? "Unknown"}'", innerException)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        RequestType = requestType;
        ExpectedHandlerType = expectedHandlerType;

        WithContext("RequestType", requestType.FullName);
        AddTypedContext("ExpectedHandlerType", expectedHandlerType);
    }
}

/// <summary>
/// Exception thrown when method resolution fails via reflection during mediator operations.
/// This typically occurs when the mediator attempts to dynamically invoke handler methods
/// but cannot locate the expected method signature on the target type.
/// </summary>
/// <remarks>
/// <para>
/// This exception is primarily used internally by the mediator when it performs reflection-based
/// method resolution for handler invocation. It can occur due to:
/// </para>
/// <list type="bullet">
/// <item><description>Missing methods on handler types</description></item>
/// <item><description>Incorrect method signatures</description></item>
/// <item><description>Generic type parameter resolution failures</description></item>
/// <item><description>Accessibility issues with handler methods</description></item>
/// </list>
/// <para>
/// The exception provides detailed information about the target type, method name,
/// and expected parameter types to facilitate debugging reflection-related issues.
/// </para>
/// </remarks>
public sealed class MethodResolutionException : SymphonyException
{
    /// <summary>
    /// Gets the unique error code for method resolution exceptions.
    /// </summary>
    /// <value>Always returns "SYMPHONY_METHOD_RESOLUTION_FAILED".</value>
    public override string ErrorCode => "SYMPHONY_METHOD_RESOLUTION_FAILED";

    /// <summary>
    /// Gets the type where method resolution failed
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Gets the method name that failed to resolve
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the expected parameter types (if specified)
    /// </summary>
    public Type[]? ExpectedParameterTypes { get; }

    /// <summary>
    /// Gets a hint on how to resolve this exception
    /// </summary>
    public override string ResolutionHint =>
        ExpectedParameterTypes != null
            ? $"Ensure that '{TargetType.Name}' has a public method '{MethodName}' with signature: ({string.Join(", ", ExpectedParameterTypes.Select(t => t.Name))})"
            : $"Ensure that '{TargetType.Name}' has a public method named '{MethodName}' with the correct signature";

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodResolutionException"/> class.
    /// </summary>
    /// <param name="targetType">The type where method resolution failed.</param>
    /// <param name="methodName">The name of the method that could not be resolved.</param>
    /// <remarks>
    /// Use this constructor when method resolution fails due to a missing method
    /// without specific parameter type requirements.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when targetType is null</exception>
    /// <exception cref="ArgumentException">Thrown when methodName is null or whitespace</exception>
    public MethodResolutionException(Type targetType, string methodName)
        : base($"Method '{methodName}' not found on type '{targetType?.Name ?? "Unknown"}'")
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        TargetType = targetType;
        MethodName = methodName;

        WithContext("TargetType", targetType.FullName)
            .WithContext("MethodName", methodName);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodResolutionException"/> class with parameter type information.
    /// </summary>
    /// <param name="targetType">The type where method resolution failed.</param>
    /// <param name="methodName">The name of the method that could not be resolved.</param>
    /// <param name="expectedParameterTypes">The expected parameter types for the method signature.</param>
    /// <remarks>
    /// Use this constructor when method resolution fails due to a missing method with
    /// a specific signature. This provides more detailed diagnostic information.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when targetType or expectedParameterTypes is null</exception>
    /// <exception cref="ArgumentException">Thrown when methodName is null or whitespace</exception>
    public MethodResolutionException(Type targetType, string methodName, Type[] expectedParameterTypes)
        : base($"Method '{methodName}' with expected signature not found on type '{targetType?.Name ?? "Unknown"}'")
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(expectedParameterTypes);

        TargetType = targetType;
        MethodName = methodName;
        ExpectedParameterTypes = expectedParameterTypes;

        WithContext("TargetType", targetType.FullName)
            .WithContext("MethodName", methodName)
            .WithContext("ExpectedParameterTypes", string.Join(", ", expectedParameterTypes.Select(t => t.Name)));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodResolutionException"/> class with an inner exception.
    /// </summary>
    /// <param name="targetType">The type where method resolution failed.</param>
    /// <param name="methodName">The name of the method that could not be resolved.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <remarks>
    /// Use this constructor when method resolution fails due to an underlying exception
    /// during the reflection process.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when targetType is null</exception>
    /// <exception cref="ArgumentException">Thrown when methodName is null or whitespace</exception>
    public MethodResolutionException(Type targetType, string methodName, Exception innerException)
        : base($"Method '{methodName}' resolution failed on type '{targetType?.Name ?? "Unknown"}'", innerException)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        TargetType = targetType;
        MethodName = methodName;

        WithContext("TargetType", targetType.FullName)
            .WithContext("MethodName", methodName);
    }
}

/// <summary>
/// Exception thrown when handler validation fails during registration or discovery.
/// This occurs when a handler type does not meet the required interface contracts
/// or validation rules for proper mediator integration.
/// </summary>
/// <remarks>
/// <para>
/// Handler validation ensures that registered types properly implement the required
/// interfaces and follow the expected patterns for request handlers, event handlers,
/// and pipeline behaviors.
/// </para>
/// <para>
/// Common validation failures include:
/// </para>
/// <list type="bullet">
/// <item><description>Types that don't implement required interfaces</description></item>
/// <item><description>Abstract or interface types registered as concrete handlers</description></item>
/// <item><description>Generic type definitions used inappropriately</description></item>
/// <item><description>Invalid constructor signatures</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // This would cause a HandlerValidationException
/// services.AddRequestHandler(typeof(IRequestHandler&lt;,&gt;)); // Interface type instead of concrete implementation
/// </code>
/// </example>
public sealed class HandlerValidationException : SymphonyException
{
    /// <summary>
    /// Gets the unique error code for handler validation exceptions.
    /// </summary>
    /// <value>Always returns "SYMPHONY_HANDLER_VALIDATION_FAILED".</value>
    public override string ErrorCode => "SYMPHONY_HANDLER_VALIDATION_FAILED";

    /// <summary>
    /// Gets the handler type that failed validation
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Gets the validation rule that failed
    /// </summary>
    public string ValidationRule { get; }

    /// <summary>
    /// Gets a hint on how to resolve this exception
    /// </summary>
    public override string ResolutionHint =>
        $"Verify that '{HandlerType.Name}' implements the required interfaces and follows the mediator handler pattern. Check the validation rule: {ValidationRule}";

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerValidationException"/> class.
    /// </summary>
    /// <param name="handlerType">The handler type that failed validation.</param>
    /// <param name="validationRule">A description of the validation rule that failed.</param>
    /// <param name="message">The detailed error message explaining the validation failure.</param>
    /// <remarks>
    /// This constructor automatically populates the exception context with the handler type
    /// and validation rule information for debugging purposes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when handlerType is null</exception>
    /// <exception cref="ArgumentException">Thrown when validationRule or message is null or whitespace</exception>
    public HandlerValidationException(Type handlerType, string validationRule, string message)
        : base($"Handler validation failed for type '{handlerType?.Name ?? "Unknown"}': {message}")
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(validationRule);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        HandlerType = handlerType;
        ValidationRule = validationRule;

        WithContext("HandlerType", handlerType.FullName)
            .WithContext("ValidationRule", validationRule);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerValidationException"/> class with an inner exception.
    /// </summary>
    /// <param name="handlerType">The handler type that failed validation.</param>
    /// <param name="validationRule">A description of the validation rule that failed.</param>
    /// <param name="message">The detailed error message explaining the validation failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <remarks>
    /// This constructor automatically populates the exception context with the handler type
    /// and validation rule information for debugging purposes.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when handlerType is null</exception>
    /// <exception cref="ArgumentException">Thrown when validationRule or message is null or whitespace</exception>
    public HandlerValidationException(Type handlerType, string validationRule, string message, Exception innerException)
        : base($"Handler validation failed for type '{handlerType?.Name ?? "Unknown"}': {message}", innerException)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(validationRule);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        HandlerType = handlerType;
        ValidationRule = validationRule;

        WithContext("HandlerType", handlerType.FullName)
            .WithContext("ValidationRule", validationRule);
    }
}