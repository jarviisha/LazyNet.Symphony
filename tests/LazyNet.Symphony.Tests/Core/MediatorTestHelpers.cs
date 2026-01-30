using LazyNet.Symphony.Core;
using LazyNet.Symphony.Interfaces;

namespace LazyNet.Symphony.Tests.Core;

/// <summary>
/// Test helper classes for MediatorTests.
/// Contains request/response types, handlers, events, and behaviors used across multiple tests.
/// </summary>

#region Test Requests and Handlers

/// <summary>
/// Simple test request that returns a string response.
/// </summary>
public record TestRequest(string Message = "Default") : IRequest<string>;

/// <summary>
/// Handler for TestRequest.
/// </summary>
public class TestRequestHandler : IRequestHandler<TestRequest, string>
{
    public int ExecutionCount { get; private set; }

    public Task<string> Handle(TestRequest request, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        return Task.FromResult($"Handled: {request.Message}");
    }
}

/// <summary>
/// Request that simulates long-running operation for cancellation testing.
/// </summary>
public record DelayRequest(int DelayMs) : IRequest<string>;

/// <summary>
/// Handler that respects cancellation token.
/// </summary>
public class DelayRequestHandler : IRequestHandler<DelayRequest, string>
{
    public async Task<string> Handle(DelayRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(request.DelayMs, cancellationToken);
        return "Completed";
    }
}

/// <summary>
/// Request that always throws an exception.
/// </summary>
public record ThrowingRequest : IRequest<string>;

/// <summary>
/// Handler that always throws an exception.
/// </summary>
public class ThrowingRequestHandler : IRequestHandler<ThrowingRequest, string>
{
    public Task<string> Handle(ThrowingRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler exception");
    }
}

#endregion

#region Test Events and Handlers

/// <summary>
/// Simple test event.
/// </summary>
public record TestEvent(string Name);

/// <summary>
/// First event handler that tracks execution.
/// </summary>
public class TestEventHandler1 : IEventHandler<TestEvent>
{
    public int ExecutionCount { get; private set; }
    public TestEvent? LastEvent { get; private set; }

    public Task Handle(TestEvent @event, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        LastEvent = @event;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Second event handler that tracks execution.
/// </summary>
public class TestEventHandler2 : IEventHandler<TestEvent>
{
    public int ExecutionCount { get; private set; }
    public TestEvent? LastEvent { get; private set; }

    public Task Handle(TestEvent @event, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        LastEvent = @event;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Event handler that throws an exception.
/// </summary>
public class ThrowingEventHandler : IEventHandler<TestEvent>
{
    public Task Handle(TestEvent @event, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Event handler exception");
    }
}

#endregion

#region Test Pipeline Behaviors

/// <summary>
/// Pipeline behavior that adds prefix to response.
/// </summary>
public class PrefixBehavior : IPipelineBehavior<TestRequest, string>
{
    public int ExecutionCount { get; private set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        var result = await next();
        return $"[Prefix] {result}";
    }
}

/// <summary>
/// Pipeline behavior that adds suffix to response.
/// </summary>
public class SuffixBehavior : IPipelineBehavior<TestRequest, string>
{
    public int ExecutionCount { get; private set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        var result = await next();
        return $"{result} [Suffix]";
    }
}

/// <summary>
/// Pipeline behavior that throws an exception before calling next.
/// </summary>
public class ThrowingBehavior : IPipelineBehavior<TestRequest, string>
{
    public Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Behavior exception");
    }
}

/// <summary>
/// Pipeline behavior that validates cancellation token.
/// </summary>
public class CancellationAwareBehavior : IPipelineBehavior<TestRequest, string>
{
    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await next();
        return result;
    }
}

#endregion
