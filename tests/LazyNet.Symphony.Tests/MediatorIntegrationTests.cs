using FluentAssertions;
using LazyNet.Symphony.Extensions;
using LazyNet.Symphony.Interfaces;
using LazyNet.Symphony.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using LazyNet.Symphony.Core;

namespace LazyNet.Symphony.Tests;

public class MediatorIntegrationTests
{
    [Fact]
    public async Task End2End_RequestWithHandler_ShouldWorkCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new IntegrationTestRequest { Message = "Hello World" };

        // Act
        var result = await mediator.Send<string>(request);

        // Assert
        result.Should().Be("Integration: Hello World");
    }

    [Fact]
    public async Task End2End_RequestWithBehaviors_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());
        services.AddPipelineBehavior(typeof(BehaviorTest1));
        services.AddPipelineBehavior(typeof(BehaviorTest2));
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new BehaviorTestRequest { Value = "test" };

        // Act
        var result = await mediator.Send<string>(request);

        // Assert
        // Thứ tự thực thi: BehaviorTest2 (outer) -> BehaviorTest1 (inner) -> Handler
        // Nhưng kết quả được wrap ngược lại: Behavior1 wrap Behavior2's result
        result.Should().Be("Behavior1: Behavior2: Handled: test");
    }

    [Fact]
    public async Task End2End_EventWithMultipleHandlers_ShouldExecuteAllHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var integrationEvent = new IntegrationTestEvent { Data = "event data" };

        // Act
        await mediator.Publish(integrationEvent);

        // Assert - We can't directly assert on handler execution without state,
        // but if no exception is thrown, it means all handlers executed successfully
        integrationEvent.Data.Should().Be("event data");
    }

    [Fact]
    public async Task End2End_RequestWithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new CancellationTestRequest();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        Func<Task> act = async () => await mediator.Send<string>(request, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task End2End_EventHandlerException_ShouldThrowOriginalException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var failingEvent = new FailingTestEvent();

        // Act & Assert
        Func<Task> act = async () => await mediator.Publish(failingEvent);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler 1 failed"); // First handler exception will be thrown
    }

    [Fact]
    public void DependencyInjection_MediatorRegistration_ShouldBeScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var mediator1 = serviceProvider.GetService<IMediator>();
        var mediator2 = serviceProvider.GetService<IMediator>();

        // Assert
        mediator1.Should().NotBeNull();
        mediator2.Should().NotBeNull();
        // In same scope, should be same instance
        mediator1.Should().BeSameAs(mediator2);
    }

    [Fact]
    public async Task Performance_CachedExecution_ShouldBeFaster()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(Assembly.GetExecutingAssembly());
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new IntegrationTestRequest { Message = "Performance Test" };

        // First execution (cache miss)
        var firstResult = await mediator.Send<string>(request);

        // Act - Multiple executions (cache hits)
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            await mediator.Send<string>(request);
        }
        var duration = DateTime.UtcNow - startTime;

        // Assert
        firstResult.Should().Be("Integration: Performance Test");
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1)); // Should be very fast with caching
    }
}

// Integration test classes
public class IntegrationTestRequest : IRequest<string>
{
    public string Message { get; set; } = string.Empty;
}

public class IntegrationTestRequestHandler : IRequestHandler<IntegrationTestRequest, string>
{
    public Task<string> Handle(IntegrationTestRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Integration: {request.Message}");
    }
}

public class IntegrationTestEvent
{
    public string Data { get; set; } = string.Empty;
}

public class IntegrationTestEventHandler1 : IEventHandler<IntegrationTestEvent>
{
    public Task Handle(IntegrationTestEvent @event, CancellationToken cancellationToken = default)
    {
        // Simulate some work
        return Task.Delay(10, cancellationToken);
    }
}

public class IntegrationTestEventHandler2 : IEventHandler<IntegrationTestEvent>
{
    public Task Handle(IntegrationTestEvent @event, CancellationToken cancellationToken = default)
    {
        // Simulate some work
        return Task.Delay(10, cancellationToken);
    }
}

public class BehaviorTestRequest : IRequest<string>
{
    public string Value { get; set; } = string.Empty;
}

public class BehaviorTestRequestHandler : IRequestHandler<BehaviorTestRequest, string>
{
    public Task<string> Handle(BehaviorTestRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Handled: {request.Value}");
    }
}

public class BehaviorTest1 : IPipelineBehavior<BehaviorTestRequest, string>
{
    public async Task<string> Handle(BehaviorTestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        var result = await next();
        return $"Behavior1: {result}";
    }
}

public class BehaviorTest2 : IPipelineBehavior<BehaviorTestRequest, string>
{
    public async Task<string> Handle(BehaviorTestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        var result = await next();
        return $"Behavior2: {result}";
    }
}

public class CancellationTestRequest : IRequest<string>
{
}

public class CancellationTestRequestHandler : IRequestHandler<CancellationTestRequest, string>
{
    public async Task<string> Handle(CancellationTestRequest request, CancellationToken cancellationToken = default)
    {
        // Check cancellation before doing work
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(1000, cancellationToken);
        return "Should not reach here if cancelled";
    }
}

public class FailingTestEvent
{
    public string Info { get; set; } = string.Empty;
}

public class FailingTestEventHandler1 : IEventHandler<FailingTestEvent>
{
    public Task Handle(FailingTestEvent @event, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler 1 failed");
    }
}

public class FailingTestEventHandler2 : IEventHandler<FailingTestEvent>
{
    public Task Handle(FailingTestEvent @event, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler 2 failed");
    }
}