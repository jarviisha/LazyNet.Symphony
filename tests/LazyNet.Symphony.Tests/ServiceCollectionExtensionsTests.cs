using FluentAssertions;
using LazyNet.Symphony.Core;
using LazyNet.Symphony.Extensions;
using LazyNet.Symphony.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LazyNet.Symphony.Tests;

public class ServiceCollectionExtensionsTests
{
    private readonly IServiceCollection _services;

    public ServiceCollectionExtensionsTests()
    {
        _services = new ServiceCollection();
    }

    [Fact]
    public void AddMediator_WithAssembly_ShouldRegisterMediatorAndHandlers()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        _services.AddMediator(assembly);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        var mediator = serviceProvider.GetService<IMediator>();
        mediator.Should().NotBeNull();
        
        // Check that handlers are registered
        var requestHandler = serviceProvider.GetService<IRequestHandler<TestServiceRequest, string>>();
        requestHandler.Should().NotBeNull();
        
        var eventHandlers = serviceProvider.GetServices<IEventHandler<TestServiceEvent>>();
        eventHandlers.Should().NotBeEmpty();
    }

    [Fact]
    public void AddRequestHandlers_ShouldRegisterOnlyRequestHandlers()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        _services.AddRequestHandlers(assembly);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        var requestHandler = serviceProvider.GetService<IRequestHandler<TestServiceRequest, string>>();
        requestHandler.Should().NotBeNull();
    }

    [Fact]
    public void AddEventHandlers_ShouldRegisterOnlyEventHandlers()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        _services.AddEventHandlers(assembly);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        var eventHandlers = serviceProvider.GetServices<IEventHandler<TestServiceEvent>>();
        eventHandlers.Should().NotBeEmpty();
    }

    [Fact]
    public void AddPipelineBehaviors_ShouldRegisterOnlyPipelineBehaviors()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        _services.AddPipelineBehaviors(assembly);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TestServiceRequest, string>>();
        behaviors.Should().NotBeEmpty();
    }

    [Fact]
    public void AddMediator_WithOptions_ShouldRegisterMediatorWithConfiguration()
    {
        // Act
        _services.AddMediator(options =>
        {
            options.FromAssemblies(Assembly.GetExecutingAssembly());
        });

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        
        var mediator = serviceProvider.GetService<IMediator>();
        mediator.Should().NotBeNull();

        // Verify the handler is registered with correct lifetime
        var descriptor = _services.FirstOrDefault(x => x.ServiceType == typeof(IRequestHandler<TestServiceRequest, string>));
        descriptor?.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMediator_MultipleCalls_ShouldNotRegisterMediatorTwice()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        _services.AddMediator(assembly);
        _services.AddMediator(assembly);

        // Assert
        var mediatorServices = _services.Where(x => x.ServiceType == typeof(IMediator));
        mediatorServices.Should().HaveCount(1);
    }
}

// Test handler implementations
public class TestServiceRequest : IRequest<string>
{
    public string Message { get; set; } = string.Empty;
}

public class TestServiceRequestHandler : IRequestHandler<TestServiceRequest, string>
{
    public Task<string> Handle(TestServiceRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Handled: {request.Message}");
    }
}

public class TestServiceEvent
{
    public string Data { get; set; } = string.Empty;
}

public class TestServiceEventHandler : IEventHandler<TestServiceEvent>
{
    public Task Handle(TestServiceEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class TestServicePipelineBehavior : IPipelineBehavior<TestServiceRequest, string>
{
    public async Task<string> Handle(TestServiceRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        var result = await next();
        return $"Behavior: {result}";
    }
}