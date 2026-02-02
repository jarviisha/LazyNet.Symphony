using FluentAssertions;
using LazyNet.Symphony.Core;
using LazyNet.Symphony.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace LazyNet.Symphony.Tests.Core;

/// <summary>
/// Comprehensive tests for Pipeline Behavior functionality.
/// Tests behavior execution order, chaining, error handling, cancellation, and edge cases.
/// </summary>
public class PipelineBehaviorTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<Mediator>> _loggerMock;
    private readonly Mediator _mediator;

    public PipelineBehaviorTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<Mediator>>();
        _mediator = new Mediator(_serviceProviderMock.Object, _loggerMock.Object);
    }

    #region Execution Order Tests

    [Fact]
    public async Task Behaviors_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var executionLog = new List<string>();

        var behavior1 = new OrderTrackingBehavior("B1", executionLog);
        var behavior2 = new OrderTrackingBehavior("B2", executionLog);
        var behavior3 = new OrderTrackingBehavior("B3", executionLog);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { behavior1, behavior2, behavior3 });

        // Act
        await _mediator.Send<string>(request);

        // Assert
        executionLog.Should().Equal(
            "B1-Before", "B2-Before", "B3-Before",
            "B3-After", "B2-After", "B1-After"
        );
    }

    [Fact]
    public async Task Behaviors_ShouldWrapResponsesInCorrectOrder()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var prefix = new PrefixBehavior();
        var suffix = new SuffixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { prefix, suffix });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert - Prefix wraps (Suffix wraps Handler)
        result.Should().Be("[Prefix] Handled: Test [Suffix]");
    }

    [Fact]
    public async Task MultipleBehaviors_WithDifferentOperations_ShouldExecuteCorrectly()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var logging = new LoggingBehavior();
        var prefix = new PrefixBehavior();
        var metadata = new MetadataWrapperBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { logging, prefix, metadata });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        logging.ExecutionCount.Should().Be(1);
        prefix.ExecutionCount.Should().Be(1);
        metadata.ExecutionCount.Should().Be(1);
        logging.Logs.Should().HaveCount(2);
        logging.Logs[0].Should().Contain("Before: Test");
        logging.Logs[1].Should().Contain("After:");
    }

    #endregion

    #region Short-Circuit Tests

    [Fact]
    public async Task Behavior_CanShortCircuitPipeline()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var shortCircuit = new ShortCircuitBehavior { ShouldShortCircuit = true };
        var suffix = new SuffixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { shortCircuit, suffix });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("Short-circuited");
        shortCircuit.NextCalled.Should().BeFalse();
        suffix.ExecutionCount.Should().Be(0); // Should not be executed
        handler.ExecutionCount.Should().Be(0); // Handler should not be called
    }

    [Fact]
    public async Task Behavior_WithoutShortCircuit_ShouldContinuePipeline()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var shortCircuit = new ShortCircuitBehavior { ShouldShortCircuit = false };
        var suffix = new SuffixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { shortCircuit, suffix });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("Handled: Test [Suffix]");
        shortCircuit.NextCalled.Should().BeTrue();
        suffix.ExecutionCount.Should().Be(1);
        handler.ExecutionCount.Should().Be(1);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task Behavior_ThrowingBeforeNext_ShouldPreventSubsequentExecution()
    {
        // Arrange
        var request = new TestRequest();
        var handler = new TestRequestHandler();
        var throwing = new ThrowingBehavior();
        var suffix = new SuffixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { throwing, suffix });

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Behavior exception");

        suffix.ExecutionCount.Should().Be(0);
        handler.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task Behavior_ThrowingAfterNext_ShouldPropagateException()
    {
        // Arrange
        var request = new TestRequest();
        var handler = new TestRequestHandler();
        var postThrow = new PostProcessThrowingBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { postThrow });

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Post-process exception");

        postThrow.NextCalled.Should().BeTrue();
        handler.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task Behavior_ValidationFailure_ShouldPreventHandlerExecution()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var validation = new ValidationBehavior { ShouldFail = true };

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { validation });

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Validation failed");

        validation.ExecutionCount.Should().Be(1);
        handler.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task Behavior_ValidationSuccess_ShouldContinuePipeline()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var validation = new ValidationBehavior { ShouldFail = false };

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { validation });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("Handled: Test");
        validation.ExecutionCount.Should().Be(1);
        handler.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task Behavior_ExceptionInMiddle_ShouldNotExecuteSubsequentBehaviors()
    {
        // Arrange
        var request = new TestRequest();
        var handler = new TestRequestHandler();
        var prefix = new PrefixBehavior();
        var throwing = new ThrowingBehavior();
        var suffix = new SuffixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { prefix, throwing, suffix });

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<InvalidOperationException>();

        prefix.ExecutionCount.Should().Be(1);
        suffix.ExecutionCount.Should().Be(0);
        handler.ExecutionCount.Should().Be(0);
    }

    #endregion

    #region Async Behavior Tests

    [Fact]
    public async Task AsyncBehavior_ShouldExecuteAsynchronously()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var asyncBehavior = new AsyncBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { asyncBehavior });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("[Async] Handled: Test");
        asyncBehavior.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task MultipleAsyncBehaviors_ShouldExecuteSequentially()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var async1 = new AsyncBehavior();
        var async2 = new AsyncBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { async1, async2 });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("[Async] [Async] Handled: Test");
        async1.ExecutionCount.Should().Be(1);
        async2.ExecutionCount.Should().Be(1);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Behavior_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var request = new TestRequest();
        var handler = new TestRequestHandler();
        var cancellationAware = new CancellationAwareBehavior();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { cancellationAware });

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AsyncBehavior_WithCancellation_ShouldCancelGracefully()
    {
        // Arrange
        var request = new DelayRequest(5000);
        var handler = new DelayRequestHandler();
        var delayBehavior = new DelayBehavior();
        var cts = new CancellationTokenSource();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<DelayRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<DelayRequest, string>>)))
            .Returns(new IPipelineBehavior<DelayRequest, string>[] { delayBehavior });

        // Act
        var task = _mediator.Send<string>(request, cts.Token);
        cts.CancelAfter(100);

        // Assert
        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        delayBehavior.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task MultipleBehaviors_WithCancellation_ShouldStopAtFirstCheck()
    {
        // Arrange
        var request = new DelayRequest(1000);
        var handler = new DelayRequestHandler();
        var delay1 = new DelayBehavior { PreDelayMs = 100 };
        var delay2 = new DelayBehavior { PreDelayMs = 100 };
        var cts = new CancellationTokenSource();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<DelayRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<DelayRequest, string>>)))
            .Returns(new IPipelineBehavior<DelayRequest, string>[] { delay1, delay2 });

        // Act
        var task = _mediator.Send<string>(request, cts.Token);
        cts.CancelAfter(150); // Cancel during delay1

        // Assert
        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Conditional Execution Tests

    [Fact]
    public async Task ConditionalBehavior_WhenEnabled_ShouldModifyResponse()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var conditional = new ConditionalBehavior { IsEnabled = true };

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { conditional });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("[Conditional] Handled: Test");
        conditional.ExecutionCount.Should().Be(1);
        conditional.BypassCount.Should().Be(0);
    }

    [Fact]
    public async Task ConditionalBehavior_WhenDisabled_ShouldBypass()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var conditional = new ConditionalBehavior { IsEnabled = false };

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { conditional });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("Handled: Test");
        conditional.ExecutionCount.Should().Be(1);
        conditional.BypassCount.Should().Be(1);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public async Task Behaviors_ShouldMaintainIndependentState()
    {
        // Arrange
        var request1 = new TestRequest("First");
        var request2 = new TestRequest("Second");
        var handler = new TestRequestHandler();
        var behavior = new PrefixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { behavior });

        // Act
        var result1 = await _mediator.Send<string>(request1);
        var result2 = await _mediator.Send<string>(request2);

        // Assert
        result1.Should().Be("[Prefix] Handled: First");
        result2.Should().Be("[Prefix] Handled: Second");
        behavior.ExecutionCount.Should().Be(2);
    }

    [Fact]
    public async Task LoggingBehavior_ShouldAccumulateLogs()
    {
        // Arrange
        var request1 = new TestRequest("First");
        var request2 = new TestRequest("Second");
        var handler = new TestRequestHandler();
        var logging = new LoggingBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { logging });

        // Act
        await _mediator.Send<string>(request1);
        await _mediator.Send<string>(request2);

        // Assert
        logging.Logs.Should().HaveCount(4); // 2 requests × (before + after)
        logging.ExecutionCount.Should().Be(2);
        logging.Logs[0].Should().Contain("Before: First");
        logging.Logs[2].Should().Contain("Before: Second");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task EmptyBehaviorCollection_ShouldExecuteHandlerDirectly()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(Array.Empty<IPipelineBehavior<TestRequest, string>>());

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("Handled: Test");
        handler.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task SingleBehavior_ShouldExecuteCorrectly()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var behavior = new PrefixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { behavior });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("[Prefix] Handled: Test");
        behavior.ExecutionCount.Should().Be(1);
        handler.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task ManyBehaviors_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var executionLog = new List<string>();

        var behaviors = Enumerable.Range(1, 10)
            .Select(i => new OrderTrackingBehavior($"B{i}", executionLog))
            .ToArray<IPipelineBehavior<TestRequest, string>>();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(behaviors);

        // Act
        await _mediator.Send<string>(request);

        // Assert
        executionLog.Should().HaveCount(20); // 10 behaviors × 2 (before + after)

        // Verify before calls are in order 1..10
        for (int i = 0; i < 10; i++)
        {
            executionLog[i].Should().Be($"B{i + 1}-Before");
        }

        // Verify after calls are in reverse order 10..1
        for (int i = 0; i < 10; i++)
        {
            executionLog[10 + i].Should().Be($"B{10 - i}-After");
        }
    }

    [Fact]
    public async Task MetadataBehavior_ShouldTrackExecutionTime()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var metadata = new MetadataWrapperBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { metadata });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Contain("[Meta:");
        result.Should().Contain("ms]");
        result.Should().Contain("Handled: Test");
        metadata.ExecutionCount.Should().Be(1);
        metadata.LastExecutionTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task ComplexPipeline_WithValidationLoggingAndMetadata_ShouldExecuteCorrectly()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var validation = new ValidationBehavior { ShouldFail = false };
        var logging = new LoggingBehavior();
        var metadata = new MetadataWrapperBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { validation, logging, metadata });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Contain("[Meta:");
        result.Should().Contain("Handled: Test");
        validation.ExecutionCount.Should().Be(1);
        logging.ExecutionCount.Should().Be(1);
        metadata.ExecutionCount.Should().Be(1);
        logging.Logs.Should().HaveCount(2);
    }

    [Fact]
    public async Task ComplexPipeline_WithShortCircuit_ShouldSkipRemainingBehaviors()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var logging1 = new LoggingBehavior();
        var shortCircuit = new ShortCircuitBehavior { ShouldShortCircuit = true };
        var logging2 = new LoggingBehavior();
        var metadata = new MetadataWrapperBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { logging1, shortCircuit, logging2, metadata });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("Short-circuited");
        logging1.ExecutionCount.Should().Be(1);
        shortCircuit.ExecutionCount.Should().Be(1);
        logging2.ExecutionCount.Should().Be(0);
        metadata.ExecutionCount.Should().Be(0);
        handler.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task MixedBehaviors_SyncAndAsync_ShouldExecuteCorrectly()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var prefix = new PrefixBehavior(); // Sync-style
        var async = new AsyncBehavior(); // Async with delays
        var suffix = new SuffixBehavior(); // Sync-style

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { prefix, async, suffix });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("[Prefix] [Async] Handled: Test [Suffix]");
        prefix.ExecutionCount.Should().Be(1);
        async.ExecutionCount.Should().Be(1);
        suffix.ExecutionCount.Should().Be(1);
        handler.ExecutionCount.Should().Be(1);
    }

    #endregion
}
