using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using LazyNet.Symphony.Exceptions;
using LazyNet.Symphony.Interfaces;

namespace LazyNet.Symphony.Extensions;

/// <summary>
/// Configuration options for Mediator handler registration, providing a fluent API
/// for configuring handlers, behaviors, and their service lifetimes.
/// </summary>
/// <remarks>
/// This class supports:
/// <list type="bullet">
/// <item><description>Assembly scanning for automatic handler discovery</description></item>
/// <item><description>Explicit handler registration with custom lifetimes</description></item>
/// <item><description>Request handlers, event handlers, and pipeline behaviors</description></item>
/// <item><description>Fluent configuration API with method chaining</description></item>
/// </list>
/// </remarks>
public class MediatorOptions
{
    /// <summary>
    /// Gets the collection of assemblies to be scanned for handler discovery.
    /// </summary>
    internal List<Assembly> Assemblies { get; } = new();
    
    /// <summary>
    /// Gets the collection of explicitly registered request handlers with their optional custom lifetimes.
    /// </summary>
    internal List<(Type HandlerType, ServiceLifetime? Lifetime)> ExplicitRequestHandlers { get; } = new();
    
    /// <summary>
    /// Gets the collection of explicitly registered event handlers with their optional custom lifetimes.
    /// </summary>
    internal List<(Type HandlerType, ServiceLifetime? Lifetime)> ExplicitEventHandlers { get; } = new();
    
    /// <summary>
    /// Gets the collection of explicitly registered pipeline behaviors with their optional custom lifetimes.
    /// </summary>
    internal List<(Type HandlerType, ServiceLifetime? Lifetime)> ExplicitPipelineBehaviors { get; } = new();
    
    /// <summary>
    /// Gets or sets the default service lifetime for request handlers when not explicitly specified.
    /// </summary>
    /// <value>The default lifetime is <see cref="ServiceLifetime.Scoped"/>.</value>
    internal ServiceLifetime DefaultRequestHandlerLifetime { get; set; } = ServiceLifetime.Scoped;
    
    /// <summary>
    /// Gets or sets the default service lifetime for event handlers when not explicitly specified.
    /// </summary>
    /// <value>The default lifetime is <see cref="ServiceLifetime.Scoped"/>.</value>
    internal ServiceLifetime DefaultEventHandlerLifetime { get; set; } = ServiceLifetime.Scoped;
    
    /// <summary>
    /// Gets or sets the default service lifetime for pipeline behaviors when not explicitly specified.
    /// </summary>
    /// <value>The default lifetime is <see cref="ServiceLifetime.Transient"/>.</value>
    internal ServiceLifetime DefaultPipelineBehaviorLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Adds assemblies to scan for handlers during service registration.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for request handlers, event handlers, and pipeline behaviors.</param>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <remarks>
    /// The mediator will automatically discover and register all classes implementing:
    /// <list type="bullet">
    /// <item><description><see cref="IRequestHandler{TRequest, TResponse}"/></description></item>
    /// <item><description><see cref="IEventHandler{TEvent}"/></description></item>
    /// <item><description><see cref="IPipelineBehavior{TRequest, TResponse}"/></description></item>
    /// </list>
    /// </remarks>
    public MediatorOptions FromAssemblies(params Assembly[] assemblies)
    {
        Assemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Adds assemblies containing the specified marker types to the scanning list.
    /// This is a convenience method to add assemblies without explicitly loading them.
    /// </summary>
    /// <param name="markerTypes">Types whose containing assemblies will be scanned for handlers.</param>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <remarks>
    /// This method extracts the assemblies from the provided types and adds them to the scanning list.
    /// Duplicate assemblies are automatically filtered out.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Add assembly containing MyHandler type
    /// options.FromAssemblyContaining&lt;MyHandler&gt;();
    /// 
    /// // Add multiple assemblies by their marker types
    /// options.FromAssemblyContaining(typeof(Handler1), typeof(Handler2));
    /// </code>
    /// </example>
    public MediatorOptions FromAssemblyContaining(params Type[] markerTypes)
    {
        var assemblies = markerTypes.Select(t => t.Assembly).Distinct();
        Assemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Adds a single request handler with default lifetime.
    /// </summary>
    /// <typeparam name="TRequestHandler">The request handler type to register.</typeparam>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <exception cref="HandlerValidationException">
    /// Thrown when the handler type is invalid (abstract, interface, or doesn't implement required interface).
    /// </exception>
    /// <example>
    /// <code>
    /// options.AddRequestHandler&lt;GetUserQueryHandler&gt;();
    /// </code>
    /// </example>
    public MediatorOptions AddRequestHandler<TRequestHandler>()
    {
        var handlerType = typeof(TRequestHandler);
        ValidateRequestHandler(handlerType);
        ExplicitRequestHandlers.Add((handlerType, null)); // null = use default lifetime
        return this;
    }

    /// <summary>
    /// Adds a single request handler with specific lifetime
    /// </summary>
    /// <typeparam name="TRequestHandler">Request handler type to register</typeparam>
    /// <param name="lifetime">Service lifetime for this handler</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddRequestHandler<TRequestHandler>(ServiceLifetime lifetime)
    {
        var handlerType = typeof(TRequestHandler);
        ValidateRequestHandler(handlerType);
        ExplicitRequestHandlers.Add((handlerType, lifetime));
        return this;
    }

    /// <summary>
    /// Adds a single request handler with default lifetime
    /// </summary>
    /// <param name="requestHandlerType">Request handler type to register</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddRequestHandler(Type requestHandlerType)
    {
        ValidateRequestHandler(requestHandlerType);
        ExplicitRequestHandlers.Add((requestHandlerType, null)); // null = use default lifetime
        return this;
    }

    /// <summary>
    /// Adds a single request handler with specific lifetime
    /// </summary>
    /// <param name="requestHandlerType">Request handler type to register</param>
    /// <param name="lifetime">Service lifetime for this handler</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddRequestHandler(Type requestHandlerType, ServiceLifetime lifetime)
    {
        ValidateRequestHandler(requestHandlerType);
        ExplicitRequestHandlers.Add((requestHandlerType, lifetime));
        return this;
    }

    /// <summary>
    /// Adds a single event handler with default lifetime.
    /// </summary>
    /// <typeparam name="TEventHandler">The event handler type to register.</typeparam>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <exception cref="HandlerValidationException">
    /// Thrown when the handler type is invalid (abstract, interface, or doesn't implement required interface).
    /// </exception>
    /// <example>
    /// <code>
    /// options.AddEventHandler&lt;UserCreatedEventHandler&gt;();
    /// </code>
    /// </example>
    public MediatorOptions AddEventHandler<TEventHandler>()
    {
        var handlerType = typeof(TEventHandler);
        ValidateEventHandler(handlerType);
        ExplicitEventHandlers.Add((handlerType, null)); // null = use default lifetime
        return this;
    }

    /// <summary>
    /// Adds a single event handler with specific lifetime
    /// </summary>
    /// <typeparam name="TEventHandler">Event handler type to register</typeparam>
    /// <param name="lifetime">Service lifetime for this handler</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddEventHandler<TEventHandler>(ServiceLifetime lifetime)
    {
        var handlerType = typeof(TEventHandler);
        ValidateEventHandler(handlerType);
        ExplicitEventHandlers.Add((handlerType, lifetime));
        return this;
    }

    /// <summary>
    /// Adds a single event handler with default lifetime
    /// </summary>
    /// <param name="eventHandlerType">Event handler type to register</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddEventHandler(Type eventHandlerType)
    {
        ValidateEventHandler(eventHandlerType);
        ExplicitEventHandlers.Add((eventHandlerType, null)); // null = use default lifetime
        return this;
    }

    /// <summary>
    /// Adds a single event handler with specific lifetime
    /// </summary>
    /// <param name="eventHandlerType">Event handler type to register</param>
    /// <param name="lifetime">Service lifetime for this handler</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddEventHandler(Type eventHandlerType, ServiceLifetime lifetime)
    {
        ValidateEventHandler(eventHandlerType);
        ExplicitEventHandlers.Add((eventHandlerType, lifetime));
        return this;
    }

    /// <summary>
    /// Adds a single pipeline behavior with default lifetime.
    /// </summary>
    /// <typeparam name="TPipelineBehavior">The pipeline behavior type to register.</typeparam>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <exception cref="HandlerValidationException">
    /// Thrown when the behavior type is invalid (abstract, interface, or doesn't implement required interface).
    /// </exception>
    /// <remarks>
    /// Pipeline behaviors are executed in the order they are registered (FIFO).
    /// Each behavior can intercept and modify the request/response flow.
    /// </remarks>
    /// <example>
    /// <code>
    /// options.AddPipelineBehavior&lt;ValidationBehavior&gt;();
    /// </code>
    /// </example>
    public MediatorOptions AddPipelineBehavior<TPipelineBehavior>()
    {
        var behaviorType = typeof(TPipelineBehavior);
        ValidatePipelineBehavior(behaviorType);
        ExplicitPipelineBehaviors.Add((behaviorType, null)); // null = use default lifetime
        return this;
    }

    /// <summary>
    /// Adds a single pipeline behavior with specific lifetime
    /// </summary>
    /// <typeparam name="TPipelineBehavior">Pipeline behavior type to register</typeparam>
    /// <param name="lifetime">Service lifetime for this behavior</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddPipelineBehavior<TPipelineBehavior>(ServiceLifetime lifetime)
    {
        var behaviorType = typeof(TPipelineBehavior);
        ValidatePipelineBehavior(behaviorType);
        ExplicitPipelineBehaviors.Add((behaviorType, lifetime));
        return this;
    }

    /// <summary>
    /// Adds a single pipeline behavior with default lifetime
    /// </summary>
    /// <param name="pipelineBehaviorType">Pipeline behavior type to register</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddPipelineBehavior(Type pipelineBehaviorType)
    {
        ValidatePipelineBehavior(pipelineBehaviorType);
        ExplicitPipelineBehaviors.Add((pipelineBehaviorType, null)); // null = use default lifetime
        return this;
    }

    /// <summary>
    /// Adds a single pipeline behavior with specific lifetime
    /// </summary>
    /// <param name="pipelineBehaviorType">Pipeline behavior type to register</param>
    /// <param name="lifetime">Service lifetime for this behavior</param>
    /// <returns>Options for chaining</returns>
    public MediatorOptions AddPipelineBehavior(Type pipelineBehaviorType, ServiceLifetime lifetime)
    {
        ValidatePipelineBehavior(pipelineBehaviorType);
        ExplicitPipelineBehaviors.Add((pipelineBehaviorType, lifetime));
        return this;
    }

    /// <summary>
    /// Sets the default service lifetime for all request handlers when not explicitly specified.
    /// </summary>
    /// <param name="lifetime">The default service lifetime to use for request handler registration.</param>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <remarks>
    /// This lifetime will be used for:
    /// <list type="bullet">
    /// <item><description>Request handlers discovered through assembly scanning</description></item>
    /// <item><description>Explicitly registered request handlers without specified lifetime</description></item>
    /// </list>
    /// The default value is <see cref="ServiceLifetime.Scoped"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Set all request handlers to use Singleton lifetime by default
    /// options.WithDefaultRequestHandlerLifetime(ServiceLifetime.Singleton);
    /// </code>
    /// </example>
    public MediatorOptions WithDefaultRequestHandlerLifetime(ServiceLifetime lifetime)
    {
        DefaultRequestHandlerLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Sets the default service lifetime for all event handlers when not explicitly specified.
    /// </summary>
    /// <param name="lifetime">The default service lifetime to use for event handler registration.</param>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <remarks>
    /// This lifetime will be used for:
    /// <list type="bullet">
    /// <item><description>Event handlers discovered through assembly scanning</description></item>
    /// <item><description>Explicitly registered event handlers without specified lifetime</description></item>
    /// </list>
    /// The default value is <see cref="ServiceLifetime.Scoped"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Set all event handlers to use Transient lifetime by default
    /// options.WithDefaultEventHandlerLifetime(ServiceLifetime.Transient);
    /// </code>
    /// </example>
    public MediatorOptions WithDefaultEventHandlerLifetime(ServiceLifetime lifetime)
    {
        DefaultEventHandlerLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Sets the default service lifetime for all pipeline behaviors when not explicitly specified.
    /// </summary>
    /// <param name="lifetime">The default service lifetime to use for pipeline behavior registration.</param>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <remarks>
    /// This lifetime will be used for:
    /// <list type="bullet">
    /// <item><description>Pipeline behaviors discovered through assembly scanning</description></item>
    /// <item><description>Explicitly registered pipeline behaviors without specified lifetime</description></item>
    /// </list>
    /// The default value is <see cref="ServiceLifetime.Transient"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Set all pipeline behaviors to use Singleton lifetime by default
    /// options.WithDefaultPipelineBehaviorLifetime(ServiceLifetime.Singleton);
    /// </code>
    /// </example>
    public MediatorOptions WithDefaultPipelineBehaviorLifetime(ServiceLifetime lifetime)
    {
        DefaultPipelineBehaviorLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Sets the default service lifetime for all handlers (request, event, and pipeline behaviors) when not explicitly specified.
    /// This is a convenience method that sets all three default lifetimes at once.
    /// </summary>
    /// <param name="lifetime">The default service lifetime to use for all handler types.</param>
    /// <returns>The current <see cref="MediatorOptions"/> instance for method chaining.</returns>
    /// <remarks>
    /// This method is equivalent to calling:
    /// <list type="bullet">
    /// <item><description><see cref="WithDefaultRequestHandlerLifetime(ServiceLifetime)"/></description></item>
    /// <item><description><see cref="WithDefaultEventHandlerLifetime(ServiceLifetime)"/></description></item>
    /// <item><description><see cref="WithDefaultPipelineBehaviorLifetime(ServiceLifetime)"/></description></item>
    /// </list>
    /// The default values are:
    /// <list type="bullet">
    /// <item><description>Request handlers: <see cref="ServiceLifetime.Scoped"/></description></item>
    /// <item><description>Event handlers: <see cref="ServiceLifetime.Scoped"/></description></item>
    /// <item><description>Pipeline behaviors: <see cref="ServiceLifetime.Transient"/></description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Set all handler types to use Singleton lifetime by default
    /// options.WithDefaultLifetime(ServiceLifetime.Singleton);
    /// </code>
    /// </example>
    public MediatorOptions WithDefaultLifetime(ServiceLifetime lifetime)
    {
        DefaultRequestHandlerLifetime = lifetime;
        DefaultEventHandlerLifetime = lifetime;
        DefaultPipelineBehaviorLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Validates that the specified type is a valid request handler implementation.
    /// </summary>
    /// <param name="handlerType">The type to validate as a request handler.</param>
    /// <exception cref="HandlerValidationException">
    /// Thrown when the type is not a concrete class or doesn't implement <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// </exception>
    /// <remarks>
    /// A valid request handler must:
    /// <list type="bullet">
    /// <item><description>Be a concrete class (not abstract or interface)</description></item>
    /// <item><description>Implement <see cref="IRequestHandler{TRequest, TResponse}"/> interface</description></item>
    /// </list>
    /// </remarks>
    private static void ValidateRequestHandler(Type handlerType)
    {
        if (handlerType.IsAbstract || handlerType.IsInterface)
            throw new HandlerValidationException(handlerType, "ConcreteClass", "Request handler type must be a concrete class");

        var hasRequestHandlerInterface = handlerType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

        if (!hasRequestHandlerInterface)
            throw new HandlerValidationException(handlerType, "InterfaceImplementation", "Type does not implement IRequestHandler<,>");
    }

    /// <summary>
    /// Validates that the specified type is a valid event handler implementation.
    /// </summary>
    /// <param name="handlerType">The type to validate as an event handler.</param>
    /// <exception cref="HandlerValidationException">
    /// Thrown when the type is not a concrete class or doesn't implement <see cref="IEventHandler{TEvent}"/>.
    /// </exception>
    /// <remarks>
    /// A valid event handler must:
    /// <list type="bullet">
    /// <item><description>Be a concrete class (not abstract or interface)</description></item>
    /// <item><description>Implement <see cref="IEventHandler{TEvent}"/> interface</description></item>
    /// </list>
    /// </remarks>
    private static void ValidateEventHandler(Type handlerType)
    {
        if (handlerType.IsAbstract || handlerType.IsInterface)
            throw new HandlerValidationException(handlerType, "ConcreteClass", "Event handler type must be a concrete class");

        var hasEventHandlerInterface = handlerType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

        if (!hasEventHandlerInterface)
            throw new HandlerValidationException(handlerType, "InterfaceImplementation", "Type does not implement IEventHandler<>");
    }

    /// <summary>
    /// Validates that the specified type is a valid pipeline behavior implementation.
    /// </summary>
    /// <param name="behaviorType">The type to validate as a pipeline behavior.</param>
    /// <exception cref="HandlerValidationException">
    /// Thrown when the type is not a concrete class or doesn't implement <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
    /// </exception>
    /// <remarks>
    /// A valid pipeline behavior must:
    /// <list type="bullet">
    /// <item><description>Be a concrete class (not abstract or interface)</description></item>
    /// <item><description>Implement <see cref="IPipelineBehavior{TRequest, TResponse}"/> interface</description></item>
    /// </list>
    /// Generic type definitions are allowed and skip interface validation.
    /// </remarks>
    private static void ValidatePipelineBehavior(Type behaviorType)
    {
        if (behaviorType.IsAbstract || behaviorType.IsInterface)
            throw new HandlerValidationException(behaviorType, "ConcreteClass", "Pipeline behavior type must be a concrete class");

        // Skip validation for generic type definitions
        if (behaviorType.IsGenericTypeDefinition)
            return;

        var hasBehaviorInterface = behaviorType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (!hasBehaviorInterface)
            throw new HandlerValidationException(behaviorType, "InterfaceImplementation", "Type does not implement IPipelineBehavior<,>");
    }
}
