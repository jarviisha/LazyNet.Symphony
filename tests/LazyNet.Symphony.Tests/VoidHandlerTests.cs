using FluentAssertions;
using LazyNet.Symphony.Interfaces;
using LazyNet.Symphony.Core;
using Microsoft.Extensions.DependencyInjection;
using LazyNet.Symphony.Extensions;

namespace LazyNet.Symphony.Tests;

/// <summary>
/// Tests for IRequestHandler&lt;TRequest&gt; (void handler) default implementation.
/// </summary>
public class VoidHandlerTests
{
    #region IRequest (non-generic) Tests

    [Fact]
    public void IRequest_ShouldInheritFromIRequestUnit()
    {
        // Assert
        typeof(IRequest).Should().Implement<IRequest<Unit>>();
    }

    [Fact]
    public void VoidCommand_ShouldImplementIRequest()
    {
        // Arrange
        var command = new TestVoidCommand("test");

        // Assert
        command.Should().BeAssignableTo<IRequest>();
        command.Should().BeAssignableTo<IRequest<Unit>>();
    }

    #endregion

    #region IRequestHandler<TRequest> Tests

    [Fact]
    public void VoidHandler_ShouldImplementIRequestHandlerWithUnit()
    {
        // Assert
        typeof(IRequestHandler<TestVoidCommand>).Should()
            .Implement<IRequestHandler<TestVoidCommand, Unit>>();
    }

    [Fact]
    public async Task VoidHandler_HandleAsync_ShouldBeCalled()
    {
        // Arrange
        var handler = new TestVoidCommandHandler();
        var command = new TestVoidCommand("test-data");

        // Act
        await handler.HandleAsync(command);

        // Assert
        handler.LastHandledData.Should().Be("test-data");
        handler.HandleCount.Should().Be(1);
    }

    [Fact]
    public async Task VoidHandler_Handle_ShouldCallHandleAsyncAndReturnUnit()
    {
        // Arrange
        IRequestHandler<TestVoidCommand, Unit> handler = new TestVoidCommandHandler();
        var command = new TestVoidCommand("test");

        // Act
        var result = await handler.Handle(command);

        // Assert
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task VoidHandler_Handle_ShouldPassCancellationToken()
    {
        // Arrange
        var handler = new TestVoidCommandWithCancellationHandler();
        var command = new TestVoidCommand("test");
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        IRequestHandler<TestVoidCommand, Unit> interfaceHandler = handler;
        await interfaceHandler.Handle(command, token);

        // Assert
        handler.ReceivedToken.Should().Be(token);
    }

    #endregion

    #region Integration with Mediator Tests

    [Fact]
    public async Task Mediator_Send_WithVoidHandler_ShouldReturnUnit()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(options =>
        {
            options.AddRequestHandler<TestVoidCommandHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var command = new TestVoidCommand("mediator-test");

        // Act
        var result = await mediator.Send(command);

        // Assert
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Mediator_Send_WithVoidHandler_ShouldExecuteHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestVoidCommandHandler();
        services.AddSingleton<IRequestHandler<TestVoidCommand, Unit>>(handler);
        services.AddScoped<IMediator, Mediator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var command = new TestVoidCommand("execute-test");

        // Act
        await mediator.Send(command);

        // Assert
        handler.LastHandledData.Should().Be("execute-test");
    }

    [Fact]
    public async Task Mediator_Send_WithVoidHandler_ShouldRespectCancellation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator(options =>
        {
            options.AddRequestHandler<CancellableVoidCommandHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var command = new CancellableVoidCommand();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => mediator.Send(command, cts.Token));
    }

    #endregion

    #region Test Types

    public record TestVoidCommand(string Data) : IRequest;

    public class TestVoidCommandHandler : IRequestHandler<TestVoidCommand>
    {
        public string? LastHandledData { get; private set; }
        public int HandleCount { get; private set; }

        public Task HandleAsync(TestVoidCommand request, CancellationToken cancellationToken = default)
        {
            LastHandledData = request.Data;
            HandleCount++;
            return Task.CompletedTask;
        }
    }

    public class TestVoidCommandWithCancellationHandler : IRequestHandler<TestVoidCommand>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task HandleAsync(TestVoidCommand request, CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    public record CancellableVoidCommand : IRequest;

    public class CancellableVoidCommandHandler : IRequestHandler<CancellableVoidCommand>
    {
        public Task HandleAsync(CancellableVoidCommand request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    #endregion
}
