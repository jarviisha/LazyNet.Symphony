using System.Reflection;
using LazyNet.Symphony.Core;
using LazyNet.Symphony.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LazyNet.Symphony.Extensions;

/// <summary>
/// Extension methods for registering Lazynet.Symphony Mediator and its handlers in DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Mediator service and scans the specified assembly for Request and Event handlers.
    /// Pipeline behaviors are excluded and should be registered manually to ensure proper execution order.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="assembly">The assembly to scan for handler implementations.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method automatically discovers and registers:
    /// <list type="bullet">
    /// <item><description>Classes implementing <see cref="IRequestHandler{TRequest, TResponse}"/></description></item>
    /// <item><description>Classes implementing <see cref="IEventHandler{TEvent}"/></description></item>
    /// </list>
    /// Pipeline behaviors must be registered manually using <c>services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(YourBehavior&lt;,&gt;))</c>
    /// to ensure FIFO execution order.
    /// </remarks>
    public static IServiceCollection AddMediator(this IServiceCollection services, Assembly assembly)
    {
        // Register the mediator if not already registered
        if (!services.Any(x => x.ServiceType == typeof(IMediator)))
        {
            services.AddScoped<IMediator, Mediator>();
        }

        // Register handler types from assembly (excluding PipelineBehaviors)
        services.AddRequestHandlers(assembly);
        services.AddEventHandlers(assembly);
        
        // NOTE: PipelineBehaviors should be registered manually to ensure FIFO order
        // Use services.AddScoped(typeof(IPipelineBehavior<,>), typeof(YourBehavior<,>)) instead

        return services;
    }

    /// <summary>
    /// Scans the specified assembly and registers all Request handlers with Scoped lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="assembly">The assembly to scan for request handler implementations.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method discovers concrete classes that implement <see cref="IRequestHandler{TRequest, TResponse}"/>
    /// and registers them with their corresponding interface types using Scoped lifetime.
    /// </remarks>
    public static IServiceCollection AddRequestHandlers(this IServiceCollection services, Assembly assembly)
    {
        var requestHandlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .ToList();

        foreach (var handlerType in requestHandlerTypes)
        {
            var handlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .ToList();

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddScoped(handlerInterface, handlerType);
            }
        }

        return services;
    }

    /// <summary>
    /// Scans the specified assembly and registers all Event handlers with Scoped lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="assembly">The assembly to scan for event handler implementations.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method discovers concrete classes that implement <see cref="IEventHandler{TEvent}"/>
    /// and registers them with their corresponding interface types using Scoped lifetime.
    /// Multiple handlers can be registered for the same event type.
    /// </remarks>
    public static IServiceCollection AddEventHandlers(this IServiceCollection services, Assembly assembly)
    {
        var eventHandlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)))
            .ToList();

        foreach (var handlerType in eventHandlerTypes)
        {
            var handlerInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .ToList();

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddScoped(handlerInterface, handlerType);
            }
        }

        return services;
    }

    /// <summary>
    /// Scans the specified assembly and registers all Pipeline behaviors with Scoped lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="assembly">The assembly to scan for pipeline behavior implementations.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method discovers concrete classes that implement <see cref="IPipelineBehavior{TRequest, TResponse}"/>
    /// and registers them with their corresponding interface types using Scoped lifetime.
    /// </para>
    /// <para>
    /// Pipeline behaviors are executed in the order they are registered (FIFO). 
    /// Generic type definitions are registered as open generics, while concrete implementations 
    /// are registered for their specific interface types.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPipelineBehaviors(this IServiceCollection services, Assembly assembly)
    {
        var behaviorTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)))
            .ToList();

        foreach (var behaviorType in behaviorTypes)
        {
            // Register as open generic if the behavior is generic type definition
            if (behaviorType.IsGenericTypeDefinition)
            {
                services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
            }
            else
            {
                // Register concrete implementations
                var behaviorInterfaces = behaviorType.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
                    .ToList();

                foreach (var behaviorInterface in behaviorInterfaces)
                {
                    services.AddScoped(behaviorInterface, behaviorType);
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Registers the Mediator service and handlers using fluent configuration options.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">A delegate to configure the mediator options.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method provides a fluent API for configuring handler registration with support for:
    /// <list type="bullet">
    /// <item><description>Assembly scanning with custom marker types</description></item>
    /// <item><description>Explicit handler registration with custom lifetimes</description></item>
    /// <item><description>Default lifetime configuration for discovered handlers</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorOptions> configureOptions)
    {
        var options = new MediatorOptions();
        configureOptions(options);

        // Register the mediator if not already registered
        if (!services.Any(x => x.ServiceType == typeof(IMediator)))
        {
            services.AddScoped<IMediator, Mediator>();
        }

        // Register explicit handlers with proper lifetime handling
        RegisterExplicitHandlers(services, options);

        return services;
    }

    /// <summary>
    /// Registers a specific Request handler type with the default Scoped lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="handlerType">The concrete Request handler type to register.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handlerType"/> is null.</exception>
    /// <remarks>
    /// The handler type must implement <see cref="IRequestHandler{TRequest, TResponse}"/> interface.
    /// This method automatically discovers all implemented handler interfaces and registers them.
    /// </remarks>
    public static IServiceCollection AddRequestHandler(this IServiceCollection services, Type handlerType)
    {
        return services.AddRequestHandler(handlerType, ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Registers a specific Request handler type with the specified service lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="handlerType">The concrete Request handler type to register.</param>
    /// <param name="lifetime">The service lifetime for the handler registration.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handlerType"/> is null.</exception>
    /// <remarks>
    /// The handler type must implement <see cref="IRequestHandler{TRequest, TResponse}"/> interface.
    /// This method automatically discovers all implemented handler interfaces and registers them
    /// with the specified lifetime (Singleton, Scoped, or Transient).
    /// </remarks>
    public static IServiceCollection AddRequestHandler(this IServiceCollection services, Type handlerType, ServiceLifetime lifetime)
    {
        var requestHandlerInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToList();

        foreach (var @interface in requestHandlerInterfaces)
        {
            services.Add(new ServiceDescriptor(@interface, handlerType, lifetime));
        }

        return services;
    }

    /// <summary>
    /// Registers a specific Event handler type with the default Scoped lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="handlerType">The concrete Event handler type to register.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handlerType"/> is null.</exception>
    /// <remarks>
    /// The handler type must implement <see cref="IEventHandler{TEvent}"/> interface.
    /// This method automatically discovers all implemented handler interfaces and registers them.
    /// Multiple handlers can be registered for the same event type.
    /// </remarks>
    public static IServiceCollection AddEventHandler(this IServiceCollection services, Type handlerType)
    {
        return services.AddEventHandler(handlerType, ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Registers a specific Event handler type with the specified service lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="handlerType">The concrete Event handler type to register.</param>
    /// <param name="lifetime">The service lifetime for the handler registration.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handlerType"/> is null.</exception>
    /// <remarks>
    /// The handler type must implement <see cref="IEventHandler{TEvent}"/> interface.
    /// This method automatically discovers all implemented handler interfaces and registers them
    /// with the specified lifetime (Singleton, Scoped, or Transient).
    /// Multiple handlers can be registered for the same event type.
    /// </remarks>
    public static IServiceCollection AddEventHandler(this IServiceCollection services, Type handlerType, ServiceLifetime lifetime)
    {
        var eventHandlerInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            .ToList();

        foreach (var @interface in eventHandlerInterfaces)
        {
            services.Add(new ServiceDescriptor(@interface, handlerType, lifetime));
        }

        return services;
    }

    /// <summary>
    /// Registers a specific Pipeline behavior type with the default Transient lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="behaviorType">The concrete Pipeline behavior type to register.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behaviorType"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The behavior type must implement <see cref="IPipelineBehavior{TRequest, TResponse}"/> interface.
    /// This method automatically discovers all implemented behavior interfaces and registers them.
    /// </para>
    /// <para>
    /// Pipeline behaviors are executed in registration order (FIFO). Generic type definitions are 
    /// registered as open generics, while concrete implementations are registered for their specific interface types.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPipelineBehavior(this IServiceCollection services, Type behaviorType)
    {
        return services.AddPipelineBehavior(behaviorType, ServiceLifetime.Transient);
    }

    /// <summary>
    /// Registers a specific Pipeline behavior type with the specified service lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="behaviorType">The concrete Pipeline behavior type to register.</param>
    /// <param name="lifetime">The service lifetime for the behavior registration.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behaviorType"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The behavior type must implement <see cref="IPipelineBehavior{TRequest, TResponse}"/> interface.
    /// This method automatically discovers all implemented behavior interfaces and registers them
    /// with the specified lifetime (Singleton, Scoped, or Transient).
    /// </para>
    /// <para>
    /// Pipeline behaviors are executed in registration order (FIFO). Generic type definitions are 
    /// registered as open generics, while concrete implementations are registered for their specific interface types.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPipelineBehavior(this IServiceCollection services, Type behaviorType, ServiceLifetime lifetime)
    {
        // Register as open generic if the behavior is generic type definition
        if (behaviorType.IsGenericTypeDefinition)
        {
            services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), behaviorType, lifetime));
        }
        else
        {
            // Register concrete implementations
            var behaviorInterfaces = behaviorType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
                .ToList();

            foreach (var behaviorInterface in behaviorInterfaces)
            {
                services.Add(new ServiceDescriptor(behaviorInterface, behaviorType, lifetime));
            }
        }

        return services;
    }

    /// <summary>
    /// Registers only the Mediator service without any handlers.
    /// Use this method when you want to register handlers separately or manually.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method registers the <see cref="IMediator"/> interface with <see cref="Mediator"/> implementation
    /// using Scoped lifetime. Handlers must be registered separately using other extension methods.
    /// The registration is idempotent - if the Mediator is already registered, it won't be registered again.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Register only the mediator
    /// services.AddMediatorOnly();
    /// 
    /// // Then register handlers separately
    /// services.AddRequestHandler(typeof(GetUserHandler));
    /// services.AddEventHandler(typeof(UserCreatedHandler));
    /// </code>
    /// </example>
    public static IServiceCollection AddMediatorOnly(this IServiceCollection services)
    {
        // Register the mediator if not already registered
        if (!services.Any(x => x.ServiceType == typeof(IMediator)))
        {
            services.AddScoped<IMediator, Mediator>();
        }

        return services;
    }

    /// <summary>
    /// Helper method that registers a handler type with all its implemented interfaces using the specified lifetime.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="handlerType">The handler type to register.</param>
    /// <param name="lifetime">The service lifetime to use for registration.</param>
    /// <remarks>
    /// This method automatically detects and registers:
    /// <list type="bullet">
    /// <item><description>Request handler interfaces (<see cref="IRequestHandler{TRequest, TResponse}"/>)</description></item>
    /// <item><description>Event handler interfaces (<see cref="IEventHandler{TEvent}"/>)</description></item>
    /// <item><description>Pipeline behavior interfaces (<see cref="IPipelineBehavior{TRequest, TResponse}"/>)</description></item>
    /// </list>
    /// Generic type definitions for pipeline behaviors are registered as open generics.
    /// </remarks>
    private static void RegisterExplicitHandler(IServiceCollection services, Type handlerType, ServiceLifetime lifetime)
    {
        // Register request handlers
        var requestHandlerInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToList();

        foreach (var @interface in requestHandlerInterfaces)
        {
            services.Add(new ServiceDescriptor(@interface, handlerType, lifetime));
        }

        // Register event handlers
        var eventHandlerInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
            .ToList();

        foreach (var @interface in eventHandlerInterfaces)
        {
            services.Add(new ServiceDescriptor(@interface, handlerType, lifetime));
        }

        // Register pipeline behaviors
        var behaviorInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
            .ToList();

        foreach (var @interface in behaviorInterfaces)
        {
            if (handlerType.IsGenericTypeDefinition)
            {
                services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), handlerType, lifetime));
            }
            else
            {
                services.Add(new ServiceDescriptor(@interface, handlerType, lifetime));
            }
        }
    }

    /// <summary>
    /// Helper method that processes MediatorOptions and registers all configured handlers
    /// including assembly scanning and explicitly registered handlers.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="options">The mediator options containing handler configurations.</param>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item><description>Scans configured assemblies for handlers using default lifetimes</description></item>
    /// <item><description>Registers explicitly configured handlers with their specified or default lifetimes</description></item>
    /// </list>
    /// </remarks>
    private static void RegisterExplicitHandlers(IServiceCollection services, MediatorOptions options)
    {
        // First, scan assemblies for handlers (if any assemblies configured)
        foreach (var assembly in options.Assemblies)
        {
            RegisterHandlersFromAssembly(services, assembly, options);
        }

        // Then, register explicit request handlers (these may override assembly-scanned handlers)
        foreach (var (handlerType, specifiedLifetime) in options.ExplicitRequestHandlers)
        {
            var lifetime = specifiedLifetime ?? options.DefaultRequestHandlerLifetime;
            RegisterExplicitHandler(services, handlerType, lifetime);
        }

        // Register explicit event handlers
        foreach (var (handlerType, specifiedLifetime) in options.ExplicitEventHandlers)
        {
            var lifetime = specifiedLifetime ?? options.DefaultEventHandlerLifetime;
            RegisterExplicitHandler(services, handlerType, lifetime);
        }

        // Register explicit pipeline behaviors
        foreach (var (handlerType, specifiedLifetime) in options.ExplicitPipelineBehaviors)
        {
            var lifetime = specifiedLifetime ?? options.DefaultPipelineBehaviorLifetime;
            RegisterExplicitHandler(services, handlerType, lifetime);
        }
    }

    /// <summary>
    /// Scans an assembly and registers all handlers with the configured default lifetimes.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="assembly">The assembly to scan for handlers.</param>
    /// <param name="options">The mediator options containing default lifetime configurations.</param>
    private static void RegisterHandlersFromAssembly(IServiceCollection services, Assembly assembly, MediatorOptions options)
    {
        var types = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .ToList();

        foreach (var type in types)
        {
            // Register request handlers
            var requestHandlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .ToList();

            foreach (var @interface in requestHandlerInterfaces)
            {
                services.Add(new ServiceDescriptor(@interface, type, options.DefaultRequestHandlerLifetime));
            }

            // Register event handlers
            var eventHandlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .ToList();

            foreach (var @interface in eventHandlerInterfaces)
            {
                services.Add(new ServiceDescriptor(@interface, type, options.DefaultEventHandlerLifetime));
            }

            // Register pipeline behaviors
            var behaviorInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
                .ToList();

            foreach (var @interface in behaviorInterfaces)
            {
                if (type.IsGenericTypeDefinition)
                {
                    services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), type, options.DefaultPipelineBehaviorLifetime));
                }
                else
                {
                    services.Add(new ServiceDescriptor(@interface, type, options.DefaultPipelineBehaviorLifetime));
                }
            }
        }
    }
}
