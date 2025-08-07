using FluentAssertions;
using LazyNet.Symphony.Core;
using LazyNet.Symphony.Interfaces;
using LazyNet.Symphony.Exceptions;
using Moq;

namespace LazyNet.Symphony.Tests;

public class MediatorTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mediator _mediator;

    public MediatorTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _mediator = new Mediator(_serviceProviderMock.Object);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action act = () => new Mediator(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("serviceProvider");
    }

    [Fact]
    public async Task Send_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Send_WithValidRequestAndHandler_ShouldReturnResponse()
    {
        // Arrange
        var request = new TestRequest();
        var expectedResponse = "Test Response";
        var handlerMock = new Mock<IRequestHandler<TestRequest, string>>();

        handlerMock.Setup(h => h.Handle(request, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expectedResponse);

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
                           .Returns(handlerMock.Object);

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEnumerable<IPipelineBehavior<TestRequest, string>>)))
                           .Returns(new List<IPipelineBehavior<TestRequest, string>>());

        // Act
        var result = await _mediator.Send<string>(request);

        // Assert
        result.Should().Be(expectedResponse);
        handlerMock.Verify(h => h.Handle(request, It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task Send_WithNoHandler_ShouldThrowHandlerNotFoundException()
    {
        // Arrange
        var request = new TestRequest();

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IRequestHandler<TestRequest, string>)))
                           .Returns((IRequestHandler<TestRequest, string>?)null);

        // Act & Assert
        Func<Task> act = async () => await _mediator.Send<string>(request);
        await act.Should().ThrowAsync<HandlerNotFoundException>();
    }

    [Fact]
    public async Task Publish_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = async () => await _mediator.Publish<TestEvent>(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Publish_WithValidEventAndHandlers_ShouldExecuteAllHandlers()
    {
        // Arrange
        var testEvent = new TestEvent { Name = "Test" };
        var handler1Mock = new Mock<IEventHandler<TestEvent>>();
        var handler2Mock = new Mock<IEventHandler<TestEvent>>();

        var handlers = new List<IEventHandler<TestEvent>> { handler1Mock.Object, handler2Mock.Object };

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
                           .Returns(handlers);

        // Act
        await _mediator.Publish(testEvent);

        // Assert
        handler1Mock.Verify(h => h.Handle(testEvent, It.IsAny<CancellationToken>()), Times.Once);
        handler2Mock.Verify(h => h.Handle(testEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WithNoHandlers_ShouldNotThrow()
    {
        // Arrange
        var testEvent = new TestEvent { Name = "Test" };

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEnumerable<IEventHandler<TestEvent>>)))
                           .Returns(new List<IEventHandler<TestEvent>>());

        // Act & Assert
        Func<Task> act = async () => await _mediator.Publish(testEvent);
        await act.Should().NotThrowAsync();
    }
}

// Test classes
public class TestRequest : IRequest<string>
{
}

public class TestEvent
{
    public string Name { get; set; } = string.Empty;
}
