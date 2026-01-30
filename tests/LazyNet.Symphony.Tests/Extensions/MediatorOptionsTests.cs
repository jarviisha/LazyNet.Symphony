using FluentAssertions;
using LazyNet.Symphony.Extensions;
using LazyNet.Symphony.Interfaces;
using LazyNet.Symphony.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using LazyNet.Symphony.Core;

namespace LazyNet.Symphony.Tests.Extensions;

public class MediatorOptionsTests
{
    [Fact]
    public void FromAssemblies_ShouldAddAssembliesToCollection()
    {
        // Arrange
        var options = new MediatorOptions();
        var assembly1 = Assembly.GetExecutingAssembly();
        var assembly2 = typeof(string).Assembly;

        // Act
        var result = options.FromAssemblies(assembly1, assembly2);

        // Assert
        result.Should().BeSameAs(options);
        options.Assemblies.Should().Contain(assembly1);
        options.Assemblies.Should().Contain(assembly2);
    }

    [Fact]
    public void FromAssemblyContaining_ShouldAddAssembliesContainingTypes()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.FromAssemblyContaining(typeof(string), typeof(MediatorOptions));

        // Assert
        result.Should().BeSameAs(options);
        options.Assemblies.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void AddRequestHandler_Generic_ShouldAddHandlerWithDefaultLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddRequestHandler<TestOptionsRequestHandler>();

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitRequestHandlers.Should().Contain(x => 
            x.HandlerType == typeof(TestOptionsRequestHandler) && 
            x.Lifetime == null);
    }

    [Fact]
    public void AddRequestHandler_GenericWithLifetime_ShouldAddHandlerWithSpecifiedLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddRequestHandler<TestOptionsRequestHandler>(ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitRequestHandlers.Should().Contain(x => 
            x.HandlerType == typeof(TestOptionsRequestHandler) && 
            x.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddRequestHandler_Type_ShouldAddHandlerWithDefaultLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddRequestHandler(typeof(TestOptionsRequestHandler));

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitRequestHandlers.Should().Contain(x => 
            x.HandlerType == typeof(TestOptionsRequestHandler) && 
            x.Lifetime == null);
    }

    [Fact]
    public void AddRequestHandler_InvalidType_ShouldThrowException()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act & Assert
        Action act = () => options.AddRequestHandler(typeof(string));
        act.Should().Throw<HandlerValidationException>();
    }

    [Fact]
    public void AddEventHandler_Generic_ShouldAddHandlerWithDefaultLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddEventHandler<TestOptionsEventHandler>();

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitEventHandlers.Should().Contain(x => 
            x.HandlerType == typeof(TestOptionsEventHandler) && 
            x.Lifetime == null);
    }

    [Fact]
    public void AddEventHandler_GenericWithLifetime_ShouldAddHandlerWithSpecifiedLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddEventHandler<TestOptionsEventHandler>(ServiceLifetime.Transient);

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitEventHandlers.Should().Contain(x => 
            x.HandlerType == typeof(TestOptionsEventHandler) && 
            x.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void AddPipelineBehavior_Generic_ShouldAddBehaviorWithDefaultLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddPipelineBehavior<TestOptionsPipelineBehavior>();

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitPipelineBehaviors.Should().Contain(x => 
            x.HandlerType == typeof(TestOptionsPipelineBehavior) && 
            x.Lifetime == null);
    }

    [Fact]
    public void WithDefaultLifetime_ShouldSetDefaultLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.WithDefaultLifetime(ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.DefaultRequestHandlerLifetime.Should().Be(ServiceLifetime.Singleton);
        options.DefaultEventHandlerLifetime.Should().Be(ServiceLifetime.Singleton);
        options.DefaultPipelineBehaviorLifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void DefaultRequestHandlerLifetime_ShouldBeScoped()
    {
        // Arrange & Act
        var options = new MediatorOptions();

        // Assert
        options.DefaultRequestHandlerLifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void DefaultEventHandlerLifetime_ShouldBeScoped()
    {
        // Arrange & Act
        var options = new MediatorOptions();

        // Assert
        options.DefaultEventHandlerLifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void DefaultPipelineBehaviorLifetime_ShouldBeTransient()
    {
        // Arrange & Act
        var options = new MediatorOptions();

        // Assert
        options.DefaultPipelineBehaviorLifetime.Should().Be(ServiceLifetime.Transient);
    }

    #region Additional Tests for Full Coverage

    [Fact]
    public void AddRequestHandler_TypeWithLifetime_ShouldAddHandlerWithSpecifiedLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddRequestHandler(typeof(TestOptionsRequestHandler), ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitRequestHandlers.Should().Contain(x =>
            x.HandlerType == typeof(TestOptionsRequestHandler) &&
            x.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddRequestHandler_AbstractType_ShouldThrowException()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act & Assert
        Action act = () => options.AddRequestHandler(typeof(AbstractRequestHandler));
        act.Should().Throw<HandlerValidationException>()
            .Which.ValidationRule.Should().Be("ConcreteClass");
    }

    [Fact]
    public void AddRequestHandler_InterfaceType_ShouldThrowException()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act & Assert
        Action act = () => options.AddRequestHandler(typeof(IRequestHandler<TestOptionsRequest, string>));
        act.Should().Throw<HandlerValidationException>()
            .Which.ValidationRule.Should().Be("ConcreteClass");
    }

    [Fact]
    public void AddEventHandler_Type_ShouldAddHandlerWithDefaultLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddEventHandler(typeof(TestOptionsEventHandler));

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitEventHandlers.Should().Contain(x =>
            x.HandlerType == typeof(TestOptionsEventHandler) &&
            x.Lifetime == null);
    }

    [Fact]
    public void AddEventHandler_TypeWithLifetime_ShouldAddHandlerWithSpecifiedLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddEventHandler(typeof(TestOptionsEventHandler), ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitEventHandlers.Should().Contain(x =>
            x.HandlerType == typeof(TestOptionsEventHandler) &&
            x.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddEventHandler_InvalidType_ShouldThrowException()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act & Assert
        Action act = () => options.AddEventHandler(typeof(string));
        act.Should().Throw<HandlerValidationException>()
            .Which.ValidationRule.Should().Be("InterfaceImplementation");
    }

    [Fact]
    public void AddEventHandler_AbstractType_ShouldThrowException()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act & Assert
        Action act = () => options.AddEventHandler(typeof(AbstractEventHandler));
        act.Should().Throw<HandlerValidationException>()
            .Which.ValidationRule.Should().Be("ConcreteClass");
    }

    [Fact]
    public void AddPipelineBehavior_Type_ShouldAddBehaviorWithDefaultLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddPipelineBehavior(typeof(TestOptionsPipelineBehavior));

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitPipelineBehaviors.Should().Contain(x =>
            x.HandlerType == typeof(TestOptionsPipelineBehavior) &&
            x.Lifetime == null);
    }

    [Fact]
    public void AddPipelineBehavior_TypeWithLifetime_ShouldAddBehaviorWithSpecifiedLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddPipelineBehavior(typeof(TestOptionsPipelineBehavior), ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitPipelineBehaviors.Should().Contain(x =>
            x.HandlerType == typeof(TestOptionsPipelineBehavior) &&
            x.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddPipelineBehavior_GenericWithLifetime_ShouldAddBehaviorWithSpecifiedLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddPipelineBehavior<TestOptionsPipelineBehavior>(ServiceLifetime.Scoped);

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitPipelineBehaviors.Should().Contain(x =>
            x.HandlerType == typeof(TestOptionsPipelineBehavior) &&
            x.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddPipelineBehavior_InvalidType_ShouldThrowException()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act & Assert
        Action act = () => options.AddPipelineBehavior(typeof(string));
        act.Should().Throw<HandlerValidationException>()
            .Which.ValidationRule.Should().Be("InterfaceImplementation");
    }

    [Fact]
    public void AddPipelineBehavior_AbstractType_ShouldThrowException()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act & Assert
        Action act = () => options.AddPipelineBehavior(typeof(AbstractPipelineBehavior));
        act.Should().Throw<HandlerValidationException>()
            .Which.ValidationRule.Should().Be("ConcreteClass");
    }

    [Fact]
    public void AddPipelineBehavior_GenericTypeDefinition_ShouldSkipInterfaceValidation()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.AddPipelineBehavior(typeof(GenericPipelineBehavior<,>));

        // Assert
        result.Should().BeSameAs(options);
        options.ExplicitPipelineBehaviors.Should().Contain(x =>
            x.HandlerType == typeof(GenericPipelineBehavior<,>));
    }

    [Fact]
    public void WithDefaultRequestHandlerLifetime_ShouldSetOnlyRequestHandlerLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.WithDefaultRequestHandlerLifetime(ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.DefaultRequestHandlerLifetime.Should().Be(ServiceLifetime.Singleton);
        options.DefaultEventHandlerLifetime.Should().Be(ServiceLifetime.Scoped); // unchanged
        options.DefaultPipelineBehaviorLifetime.Should().Be(ServiceLifetime.Transient); // unchanged
    }

    [Fact]
    public void WithDefaultEventHandlerLifetime_ShouldSetOnlyEventHandlerLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.WithDefaultEventHandlerLifetime(ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.DefaultEventHandlerLifetime.Should().Be(ServiceLifetime.Singleton);
        options.DefaultRequestHandlerLifetime.Should().Be(ServiceLifetime.Scoped); // unchanged
        options.DefaultPipelineBehaviorLifetime.Should().Be(ServiceLifetime.Transient); // unchanged
    }

    [Fact]
    public void WithDefaultPipelineBehaviorLifetime_ShouldSetOnlyPipelineBehaviorLifetime()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act
        var result = options.WithDefaultPipelineBehaviorLifetime(ServiceLifetime.Singleton);

        // Assert
        result.Should().BeSameAs(options);
        options.DefaultPipelineBehaviorLifetime.Should().Be(ServiceLifetime.Singleton);
        options.DefaultRequestHandlerLifetime.Should().Be(ServiceLifetime.Scoped); // unchanged
        options.DefaultEventHandlerLifetime.Should().Be(ServiceLifetime.Scoped); // unchanged
    }

    [Fact]
    public void FromAssemblyContaining_ShouldFilterDuplicateAssemblies()
    {
        // Arrange
        var options = new MediatorOptions();

        // Act - Add same assembly type multiple times
        options.FromAssemblyContaining(typeof(MediatorOptions), typeof(MediatorOptions));

        // Assert - Should only have unique assemblies
        var symphonyAssembly = typeof(MediatorOptions).Assembly;
        options.Assemblies.Count(a => a == symphonyAssembly).Should().Be(1);
    }

    #endregion
}

// Additional test classes for validation
public abstract class AbstractRequestHandler : IRequestHandler<TestOptionsRequest, string>
{
    public abstract Task<string> Handle(TestOptionsRequest request, CancellationToken cancellationToken = default);
}

public abstract class AbstractEventHandler : IEventHandler<TestOptionsEvent>
{
    public abstract Task Handle(TestOptionsEvent @event, CancellationToken cancellationToken = default);
}

public abstract class AbstractPipelineBehavior : IPipelineBehavior<TestOptionsRequest, string>
{
    public abstract Task<string> Handle(TestOptionsRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default);
}

public class GenericPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, PipelineNext<TResponse> next, CancellationToken cancellationToken = default)
    {
        return next();
    }
}

// Test classes for options
public class TestOptionsRequest : IRequest<string>
{
    public string Data { get; set; } = string.Empty;
}

public class TestOptionsRequestHandler : IRequestHandler<TestOptionsRequest, string>
{
    public Task<string> Handle(TestOptionsRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Options: {request.Data}");
    }
}

public class TestOptionsEvent
{
    public string Info { get; set; } = string.Empty;
}

public class TestOptionsEventHandler : IEventHandler<TestOptionsEvent>
{
    public Task Handle(TestOptionsEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class TestOptionsPipelineBehavior : IPipelineBehavior<TestOptionsRequest, string>
{
    public async Task<string> Handle(TestOptionsRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        var result = await next();
        return $"Options Behavior: {result}";
    }
}