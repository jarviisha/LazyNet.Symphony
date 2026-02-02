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

/// <summary>
/// Pipeline behavior that modifies the request before passing to next behavior.
/// </summary>
public class RequestModifyingBehavior : IPipelineBehavior<TestRequest, string>
{
    public int ExecutionCount { get; private set; }
    public string ModifiedMessage { get; private set; } = string.Empty;

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        ModifiedMessage = $"Modified: {request.Message}";
        var result = await next();
        return $"[Modified] {result}";
    }
}

/// <summary>
/// Pipeline behavior that tracks execution order.
/// </summary>
public class OrderTrackingBehavior : IPipelineBehavior<TestRequest, string>
{
    public string Name { get; }
    public List<string> ExecutionLog { get; }

    public OrderTrackingBehavior(string name, List<string> executionLog)
    {
        Name = name;
        ExecutionLog = executionLog;
    }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionLog.Add($"{Name}-Before");
        var result = await next();
        ExecutionLog.Add($"{Name}-After");
        return result;
    }
}

/// <summary>
/// Pipeline behavior that can short-circuit the pipeline.
/// </summary>
public class ShortCircuitBehavior : IPipelineBehavior<TestRequest, string>
{
    public bool ShouldShortCircuit { get; set; }
    public int ExecutionCount { get; private set; }
    public bool NextCalled { get; private set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;

        if (ShouldShortCircuit)
        {
            return "Short-circuited";
        }

        NextCalled = true;
        return await next();
    }
}

/// <summary>
/// Pipeline behavior that performs async operations before and after handler.
/// </summary>
public class AsyncBehavior : IPipelineBehavior<TestRequest, string>
{
    public int ExecutionCount { get; private set; }
    public int PreProcessDelayMs { get; set; } = 10;
    public int PostProcessDelayMs { get; set; } = 10;

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;

        // Simulate async pre-processing
        await Task.Delay(PreProcessDelayMs, cancellationToken);

        var result = await next();

        // Simulate async post-processing
        await Task.Delay(PostProcessDelayMs, cancellationToken);

        return $"[Async] {result}";
    }
}

/// <summary>
/// Pipeline behavior that validates request before processing.
/// </summary>
public class ValidationBehavior : IPipelineBehavior<TestRequest, string>
{
    public int ExecutionCount { get; private set; }
    public bool ShouldFail { get; set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;

        if (ShouldFail)
        {
            throw new InvalidOperationException("Validation failed");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message cannot be empty");
        }

        return await next();
    }
}

/// <summary>
/// Pipeline behavior that wraps response with metadata.
/// </summary>
public class MetadataWrapperBehavior : IPipelineBehavior<TestRequest, string>
{
    public int ExecutionCount { get; private set; }
    public TimeSpan LastExecutionTime { get; private set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        var startTime = DateTime.UtcNow;

        var result = await next();

        LastExecutionTime = DateTime.UtcNow - startTime;
        return $"[Meta: {LastExecutionTime.TotalMilliseconds}ms] {result}";
    }
}

/// <summary>
/// Pipeline behavior that logs execution.
/// </summary>
public class LoggingBehavior : IPipelineBehavior<TestRequest, string>
{
    public List<string> Logs { get; } = new();
    public int ExecutionCount { get; private set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        Logs.Add($"Before: {request.Message}");

        var result = await next();

        Logs.Add($"After: {result}");
        return result;
    }
}

/// <summary>
/// Pipeline behavior that can be conditionally enabled/disabled.
/// </summary>
public class ConditionalBehavior : IPipelineBehavior<TestRequest, string>
{
    public bool IsEnabled { get; set; } = true;
    public int ExecutionCount { get; private set; }
    public int BypassCount { get; private set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;

        if (!IsEnabled)
        {
            BypassCount++;
            return await next();
        }

        var result = await next();
        return $"[Conditional] {result}";
    }
}

/// <summary>
/// Pipeline behavior that throws exception after calling next.
/// </summary>
public class PostProcessThrowingBehavior : IPipelineBehavior<TestRequest, string>
{
    public int ExecutionCount { get; private set; }
    public bool NextCalled { get; private set; }

    public async Task<string> Handle(TestRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        var result = await next();
        NextCalled = true;
        throw new InvalidOperationException("Post-process exception");
    }
}

/// <summary>
/// Pipeline behavior for DelayRequest to test cancellation in pipeline.
/// </summary>
public class DelayBehavior : IPipelineBehavior<DelayRequest, string>
{
    public int PreDelayMs { get; set; } = 50;
    public int PostDelayMs { get; set; } = 50;
    public int ExecutionCount { get; private set; }

    public async Task<string> Handle(DelayRequest request, PipelineNext<string> next, CancellationToken cancellationToken = default)
    {
        ExecutionCount++;

        await Task.Delay(PreDelayMs, cancellationToken);
        var result = await next();
        await Task.Delay(PostDelayMs, cancellationToken);

        return $"[Delayed] {result}";
    }
}

#endregion
