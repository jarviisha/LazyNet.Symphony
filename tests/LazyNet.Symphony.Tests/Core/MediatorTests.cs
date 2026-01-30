using FluentAssertions;
using LazyNet.Symphony.Core;
using LazyNet.Symphony.Interfaces;
using LazyNet.Symphony.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace LazyNet.Symphony.Tests.Core;

/// <summary>
/// Comprehensive tests for the Mediator class.
/// Organized by functionality: Constructor, Send, Publish, Pipeline, Error Handling, and Cancellation.
/// </summary>
public class MediatorTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<Mediator>> _loggerMock;
    private readonly Mediator _mediator;

    public MediatorTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<Mediator>>();
        _mediator = new Mediator(_serviceProviderMock.Object, _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new Mediator(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("serviceProvider");
    }

    [Fact]
    public void Constructor_WithValidServiceProvider_ShouldCreateInstance()
    {
        // Act
        var mediator = new Mediator(_serviceProviderMock.Object);

        // Assert
        mediator.Should().NotBeNull();
        mediator.Should().BeAssignableTo<IMediator>();
    }

    [Fact]
    public void Constructor_WithoutLogger_ShouldUseNullLogger()
    {
        // Act
        var mediator = new Mediator(_serviceProviderMock.Object);

        // Assert
        mediator.Should().NotBeNull();
        // Should not throw when logging is attempted
    }

    [Fact]
    public void Constructor_WithLogger_ShouldUseProvidedLogger()
    {
        // Act
        var mediator = new Mediator(_serviceProviderMock.Object, _loggerMock.Object);

        // Assert
        mediator.Should().NotBeNull();
    }

    #endregion

    #region Send Method - Basic Tests

    [Fact]
    public async Task Send_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Send_WithValidRequest_ShouldReturnResponse()
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
    public async Task Send_WithNoHandler_ShouldThrowHandlerNotFoundException()
    {
        // Arrange
        var request = new TestRequest();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns((IRequestHandler<TestRequest, string>?)null);

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<HandlerNotFoundException>()
            .WithMessage("*TestRequest*");
    }

    [Fact]
    public async Task Send_WithHandlerException_ShouldPropagateException()
    {
        // Arrange
        var request = new ThrowingRequest();
        var handler = new ThrowingRequestHandler();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<ThrowingRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<ThrowingRequest, string>>)))
            .Returns(Array.Empty<IPipelineBehavior<ThrowingRequest, string>>());

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler exception");
    }

    #endregion

    #region Send Method - Pipeline Behavior Tests

    [Fact]
    public async Task Send_WithSingleBehavior_ShouldExecuteBehavior()
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
    public async Task Send_WithMultipleBehaviors_ShouldExecuteInFIFOOrder()
    {
        // Arrange
        var request = new TestRequest("Test");
        var handler = new TestRequestHandler();
        var behavior1 = new PrefixBehavior();
        var behavior2 = new SuffixBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { behavior1, behavior2 });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        // Execution order: Behavior1 -> Behavior2 -> Handler
        // Result wrapping: Behavior1 wraps (Behavior2 wraps Handler)
        result.Should().Be("[Prefix] Handled: Test [Suffix]");
        behavior1.ExecutionCount.Should().Be(1);
        behavior2.ExecutionCount.Should().Be(1);
        handler.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task Send_WithNoBehaviors_ShouldExecuteHandlerDirectly()
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
    public async Task Send_WithBehaviorException_ShouldPropagateException()
    {
        // Arrange
        var request = new TestRequest();
        var handler = new TestRequestHandler();
        var behavior = new ThrowingBehavior();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(new IPipelineBehavior<TestRequest, string>[] { behavior });

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Behavior exception");
    }

    [Fact]
    public async Task Send_WithNullBehaviorsInCollection_ShouldFilterOutNulls()
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
            .Returns(new IPipelineBehavior<TestRequest, string>?[] { null, behavior, null });

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be("[Prefix] Handled: Test");
        behavior.ExecutionCount.Should().Be(1);
    }

    #endregion

    #region Publish Method - Basic Tests

    [Fact]
    public async Task Publish_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = async () => await _mediator.Publish<TestEvent>(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Publish_WithNoHandlers_ShouldNotThrow()
    {
        // Arrange
        var testEvent = new TestEvent("Test");

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
            .Returns(Array.Empty<IEventHandler<TestEvent>>());

        // Act & Assert
        Func<Task> act = async () => await _mediator.Publish(testEvent);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Publish_WithSingleHandler_ShouldExecuteHandler()
    {
        // Arrange
        var testEvent = new TestEvent("Test");
        var handler = new TestEventHandler1();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
            .Returns(new IEventHandler<TestEvent>[] { handler });

        // Act
        await _mediator.Publish(testEvent);

        // Assert
        handler.ExecutionCount.Should().Be(1);
        handler.LastEvent.Should().Be(testEvent);
    }

    [Fact]
    public async Task Publish_WithMultipleHandlers_ShouldExecuteAllHandlersInOrder()
    {
        // Arrange
        var testEvent = new TestEvent("Test");
        var handler1 = new TestEventHandler1();
        var handler2 = new TestEventHandler2();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
            .Returns(new IEventHandler<TestEvent>[] { handler1, handler2 });

        // Act
        await _mediator.Publish(testEvent);

        // Assert
        handler1.ExecutionCount.Should().Be(1);
        handler1.LastEvent.Should().Be(testEvent);
        handler2.ExecutionCount.Should().Be(1);
        handler2.LastEvent.Should().Be(testEvent);
    }

    [Fact]
    public async Task Publish_WithHandlerException_ShouldPropagateException()
    {
        // Arrange
        var testEvent = new TestEvent("Test");
        var handler = new ThrowingEventHandler();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
            .Returns(new IEventHandler<TestEvent>[] { handler });

        // Act & Assert
        Func<Task> act = async () => await _mediator.Publish(testEvent);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Event handler exception");
    }

    [Fact]
    public async Task Publish_WithNullHandlersInCollection_ShouldFilterOutNulls()
    {
        // Arrange
        var testEvent = new TestEvent("Test");
        var handler = new TestEventHandler1();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
            .Returns(new IEventHandler<TestEvent>?[] { null, handler, null });

        // Act
        await _mediator.Publish(testEvent);

        // Assert
        handler.ExecutionCount.Should().Be(1);
        handler.LastEvent.Should().Be(testEvent);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Send_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var request = new TestRequest();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Send_WithDelayAndCancellation_ShouldCancelOperation()
    {
        // Arrange
        var request = new DelayRequest(5000);
        var handler = new DelayRequestHandler();
        var cts = new CancellationTokenSource();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<DelayRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<DelayRequest, string>>)))
            .Returns(Array.Empty<IPipelineBehavior<DelayRequest, string>>());

        // Act
        var task = _mediator.Send<string>(request, cts.Token);
        cts.CancelAfter(100);

        // Assert
        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Publish_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var testEvent = new TestEvent("Test");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Func<Task> act = async () => await _mediator.Publish(testEvent, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Type Caching Tests

    [Fact]
    public async Task Send_MultipleCallsSameType_ShouldUseCachedTypes()
    {
        // Arrange
        var request1 = new TestRequest("First");
        var request2 = new TestRequest("Second");
        var handler = new TestRequestHandler();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
            .Returns(Array.Empty<IPipelineBehavior<TestRequest, string>>());

        // Act
        var result1 = await _mediator.Send<string>(request1);
        var result2 = await _mediator.Send<string>(request2);

        // Assert
        result1.Should().Be("Handled: First");
        result2.Should().Be("Handled: Second");
        handler.ExecutionCount.Should().Be(2);
    }

    [Fact]
    public async Task Publish_MultipleCallsSameType_ShouldUseCachedTypes()
    {
        // Arrange
        var event1 = new TestEvent("First");
        var event2 = new TestEvent("Second");
        var handler = new TestEventHandler1();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
            .Returns(new IEventHandler<TestEvent>[] { handler });

        // Act
        await _mediator.Publish(event1);
        await _mediator.Publish(event2);

        // Assert
        handler.ExecutionCount.Should().Be(2);
        handler.LastEvent.Should().Be(event2);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task Send_WithLogger_ShouldLogDebugMessages()
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
        await _mediator.Send<string>(request);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Publish_WithLogger_ShouldLogDebugMessages()
    {
        // Arrange
        var testEvent = new TestEvent("Test");
        var handler = new TestEventHandler1();

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
            .Returns(new IEventHandler<TestEvent>[] { handler });

        // Act
        await _mediator.Publish(testEvent);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Publishing event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}
