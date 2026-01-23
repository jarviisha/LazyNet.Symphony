using FluentAssertions;
using LazyNet.Symphony.Core;
using LazyNet.Symphony.Exceptions;
using LazyNet.Symphony.Interfaces;

namespace LazyNet.Symphony.Tests;

public class MediatorExecutionHelperTests
{
    [Fact]
    public async Task ExecuteHandler_WithValidHandler_ShouldReturnResult()
    {
        // Arrange
        var request = new TestExecutionRequest { Data = "test" };
        var handler = new TestExecutionRequestHandler();

        // Act
        var result = await MediatorExecutionHelper.ExecuteHandler<string>(handler, request, CancellationToken.None);

        // Assert
        result.Should().Be("Handled: test");
    }

    [Fact]
    public async Task ExecuteHandler_WithNullHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        var request = new TestExecutionRequest();

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteHandler<string>(null!, request, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteHandler_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var handler = new TestExecutionRequestHandler();

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteHandler<string>(handler, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteBehavior_WithValidBehavior_ShouldReturnModifiedResult()
    {
        // Arrange
        var request = new TestExecutionRequest { Data = "test" };
        var behavior = new TestExecutionPipelineBehavior();
        var nextCalled = false;
        PipelineNext<string> next = () => 
        {
            nextCalled = true;
            return Task.FromResult("original");
        };

        // Act
        var result = await MediatorExecutionHelper.ExecuteBehavior(behavior, request, next, CancellationToken.None);

        // Assert
        result.Should().Be("Behavior: original");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteBehavior_WithNullBehavior_ShouldThrowArgumentNullException()
    {
        // Arrange
        var request = new TestExecutionRequest();
        PipelineNext<string> next = () => Task.FromResult("test");

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteBehavior<string>(null!, request, next, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteBehavior_WithNullNext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var request = new TestExecutionRequest();
        var behavior = new TestExecutionPipelineBehavior();

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteBehavior<string>(behavior, request, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteEventHandler_WithValidHandler_ShouldExecuteSuccessfully()
    {
        // Arrange
        var eventObj = new TestExecutionEvent { Message = "test event" };
        var handler = new TestExecutionEventHandler();

        // Act
        await MediatorExecutionHelper.ExecuteEventHandler(handler, eventObj, CancellationToken.None);

        // Assert
        handler.HandledEvent.Should().Be(eventObj);
    }

    [Fact]
    public async Task ExecuteEventHandler_WithNullHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        var eventObj = new TestExecutionEvent();

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteEventHandler(null!, eventObj, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteEventHandler_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var handler = new TestExecutionEventHandler();

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteEventHandler(handler, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteHandler_WithExceptionInHandler_ShouldBubbleOriginalException()
    {
        // Arrange
        var request = new TestExecutionRequest();
        var handler = new ThrowingTestRequestHandler();

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteHandler<string>(handler, request, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception from handler");
    }

    [Fact]
    public async Task ExecuteBehavior_WithExceptionInBehavior_ShouldBubbleOriginalException()
    {
        // Arrange
        var request = new TestExecutionRequest();
        var behavior = new ThrowingTestPipelineBehavior();
        PipelineNext<string> next = () => Task.FromResult("test");

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteBehavior<string>(behavior, request, next, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception from behavior");
    }

    [Fact]
    public async Task ExecuteEventHandler_WithExceptionInHandler_ShouldBubbleOriginalException()
    {
        // Arrange
        var eventObj = new TestExecutionEvent();
        var handler = new ThrowingTestEventHandler();

        // Act & Assert
        Func<Task> act = async () => await MediatorExecutionHelper.ExecuteEventHandler(handler, eventObj, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception from event handler");
    }

    [Fact]
    public void ClearCache_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        var act = MediatorExecutionHelper.ClearCache;
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ClearCache_ShouldAllowReexecution()
    {
        // Arrange
        var request = new TestExecutionRequest { Data = "test" };
        var handler = new TestExecutionRequestHandler();

        // Act - Execute, clear cache, execute again
        var result1 = await MediatorExecutionHelper.ExecuteHandler<string>(handler, request, CancellationToken.None);
        MediatorExecutionHelper.ClearCache();
        var result2 = await MediatorExecutionHelper.ExecuteHandler<string>(handler, request, CancellationToken.None);

        // Assert
        result1.Should().Be("Handled: test");
        result2.Should().Be("Handled: test");
    }
}

// Test classes for MediatorExecutionHelper tests
public class TestExecutionRequest : IRequest<string>
{
    public string Data { get; set; } = string.Empty;
}

public class TestExecutionRequestHandler : IRequestHandler<TestExecutionRequest, string>
{
    public Task<string> Handle(TestExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Handled: {request.Data}");
    }
}

public class ThrowingTestRequestHandler : IRequestHandler<TestExecutionRequest, string>
{
    public Task<string> Handle(TestExecutionRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Test exception from handler");
    }
}

public class TestExecutionPipelineBehavior : IPipelineBehavior<TestExecutionRequest, string>
{
    public async Task<string> Handle(TestExecutionRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        var result = await next();
        return $"Behavior: {result}";
    }
}

public class ThrowingTestPipelineBehavior : IPipelineBehavior<TestExecutionRequest, string>
{
    public Task<string> Handle(TestExecutionRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Test exception from behavior");
    }
}

public class TestExecutionEvent
{
    public string Message { get; set; } = string.Empty;
}

public class TestExecutionEventHandler : IEventHandler<TestExecutionEvent>
{
    public TestExecutionEvent? HandledEvent { get; private set; }

    public Task Handle(TestExecutionEvent @event, CancellationToken cancellationToken = default)
    {
        HandledEvent = @event;
        return Task.CompletedTask;
    }
}

public class ThrowingTestEventHandler : IEventHandler<TestExecutionEvent>
{
    public Task Handle(TestExecutionEvent @event, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Test exception from event handler");
    }
}