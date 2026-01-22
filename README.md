# Symphony

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![NuGet](https://img.shields.io/nuget/v/LazyNet.Symphony.svg)](https://www.nuget.org/packages/LazyNet.Symphony/)

A high-performance, lightweight implementation of the Mediator pattern for .NET applications, providing a simple interface to handle requests, commands, queries, and domain events with advanced pipeline support.

> **Beta Release**
>
> This is a **beta version (1.0.0-beta.1)** of Symphony. The core functionality is stable, tested, and ready for evaluation.
>
> - ✅ Safe for **testing** and **evaluation**
> - ✅ Core features are **stable**
> - ⚠️ Minor API changes may occur before stable release
> - 🐛 Please report any issues on [GitHub Issues](https://github.com/jarviisha/LazyNet.Symphony/issues)

## ⚡ Quick Example

```csharp
// 1. Register services
builder.Services.AddMediator(typeof(Program).Assembly);

// 2. Define request and handler
public record GetUserQuery(int Id) : IRequest<User>;

public class GetUserHandler : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _repository;

    public GetUserHandler(IUserRepository repository) => _repository = repository;

    public Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
        => _repository.GetByIdAsync(request.Id, cancellationToken);
}

// 3. Use in your controller/service
[ApiController, Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public Task<User> Get(int id) => _mediator.Send(new GetUserQuery(id));
}
```

## 📋 Prerequisites

- **.NET 8.0** or later
- **Microsoft.Extensions.DependencyInjection** (included)
- Any **.NET 8.0 compatible** project (Web API, Console, etc.)

## ✨ Features

- **🚀 High Performance**: Uses compiled delegates and advanced caching for optimal performance
- **📨 Request/Response Pattern**: CQRS-style command and query handling
- **📡 Event Publishing**: Publish domain events to multiple handlers
- **🔧 Pipeline Behaviors**: Cross-cutting concerns like validation, logging, caching
- **💉 Dependency Injection**: Seamless integration with Microsoft.Extensions.DependencyInjection
- **🛡️ Exception Handling**: Detailed exception context and error information
- **📝 Full Documentation**: Comprehensive XML documentation and examples
- **🔄 Unit Type Support**: Simplified handlers for commands without return values
- **📊 Optional Logging**: Built-in ILogger support for diagnostics
- **🔒 Deadlock Prevention**: ConfigureAwait(false) throughout for UI app safety

## 📦 Installation

### Package Manager Console
```powershell
Install-Package LazyNet.Symphony -Version 1.0.0-beta.1
```

### .NET CLI
```bash
dotnet add package LazyNet.Symphony --version 1.0.0-beta.1
```

### PackageReference
```xml
<PackageReference Include="LazyNet.Symphony" Version="1.0.0-beta.1" />
```

## 🚀 Quick Start

### 1. Register Services

```csharp
using LazyNet.Symphony.Extensions;

// In Program.cs or Startup.cs
builder.Services.AddMediator(typeof(Program).Assembly);

// Or register from multiple assemblies
builder.Services.AddMediator(Assembly.GetExecutingAssembly());

// With fluent configuration
builder.Services.AddMediator(options =>
{
    options.FromAssemblies(typeof(Program).Assembly)
           .WithDefaultRequestHandlerLifetime(ServiceLifetime.Scoped);
});
```

### 2. Define Requests and Handlers

```csharp
using LazyNet.Symphony.Interfaces;

// Define a query (with response)
public record GetUserQuery(int UserId) : IRequest<User>;

// Define a command (with response)
public record CreateUserCommand(string Name, string Email) : IRequest<int>;

// Define a command (without response - using Unit)
public record DeleteUserCommand(int UserId) : IRequest;

// Query handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _userRepository;

    public GetUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
    }
}

// Command handler (with response)
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly IUserRepository _userRepository;

    public CreateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User(request.Name, request.Email);
        return await _userRepository.CreateAsync(user, cancellationToken);
    }
}

// Command handler (without response - simplified interface)
public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _userRepository;

    public DeleteUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task HandleAsync(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        await _userRepository.DeleteAsync(request.UserId, cancellationToken);
    }
}
```

### 3. Use the Mediator

```csharp
[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{id}")]
    public async Task<User> GetUser(int id)
    {
        return await _mediator.Send(new GetUserQuery(id));
    }

    [HttpPost]
    public async Task<int> CreateUser(CreateUserCommand command)
    {
        return await _mediator.Send(command);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        await _mediator.Send(new DeleteUserCommand(id));
        return NoContent();
    }
}
```

## 📡 Event Handling

### Define and Handle Events

```csharp
// Define an event
public record UserCreatedEvent(int UserId, string Name, string Email);

// Event handler
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IEmailService _emailService;

    public UserCreatedEventHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(UserCreatedEvent @event, CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(@event.Email, @event.Name, cancellationToken);
    }
}

// Multiple handlers for the same event
public class UserCreatedLoggingHandler : IEventHandler<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedLoggingHandler> _logger;

    public UserCreatedLoggingHandler(ILogger<UserCreatedLoggingHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User created: {UserId} - {Name}", @event.UserId, @event.Name);
        return Task.CompletedTask;
    }
}
```

### Publish Events

```csharp
// In your command handler
public async Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
{
    var user = new User(request.Name, request.Email);
    var userId = await _userRepository.CreateAsync(user, cancellationToken);

    // Publish domain event
    await _mediator.Publish(new UserCreatedEvent(userId, request.Name, request.Email), cancellationToken);

    return userId;
}
```

## 🔧 Pipeline Behaviors

Pipeline behaviors allow you to implement cross-cutting concerns like validation, logging, caching, etc.

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, PipelineNext<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}", requestName);
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();
        _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}

// Register pipeline behaviors (order matters - FIFO execution)
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
```

## ⚡ Performance Features

Symphony is designed for high performance:

- **Compiled Delegates**: Uses expression compilation for fast handler invocation
- **Type Caching**: Caches generic types to avoid repeated reflection
- **Minimal Allocations**: Optimized memory usage with efficient collections
- **Concurrent Collections**: Thread-safe caching with `ConcurrentDictionary`
- **ConfigureAwait(false)**: Prevents deadlocks in UI applications

## 📊 Logging Support

Symphony includes optional logging for diagnostics:

```csharp
// Logging is automatically enabled when ILogger<Mediator> is available
// Just register your logging provider as usual

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug); // Enable debug logs to see mediator activity
});

// Log output examples:
// [DBG] Processing request GetUserQuery
// [DBG] Executing request GetUserQuery with 2 behaviors
// [DBG] Request GetUserQuery completed in 15ms
// [DBG] Publishing event UserCreatedEvent
// [DBG] Event UserCreatedEvent published to 2 handlers in 5ms
```

## 📚 Advanced Configuration

### Fluent Configuration API

```csharp
builder.Services.AddMediator(options =>
{
    options
        .FromAssemblies(typeof(Program).Assembly, typeof(Domain.Marker).Assembly)
        .WithDefaultRequestHandlerLifetime(ServiceLifetime.Scoped)
        .WithDefaultEventHandlerLifetime(ServiceLifetime.Scoped)
        .WithDefaultPipelineBehaviorLifetime(ServiceLifetime.Transient)
        .AddRequestHandler<GetUserQueryHandler>()
        .AddEventHandler<UserCreatedEventHandler>()
        .AddPipelineBehavior<LoggingBehavior<,>>();
});
```

### Manual Handler Registration

```csharp
// Register specific handlers manually
services.AddScoped<IRequestHandler<GetUserQuery, User>, GetUserQueryHandler>();
services.AddScoped<IEventHandler<UserCreatedEvent>, UserCreatedEventHandler>();

// Register handlers for void commands
services.AddScoped<IRequestHandler<DeleteUserCommand, Unit>, DeleteUserCommandHandler>();
```

## 🚨 Exception Handling

Symphony provides detailed exception information:

```csharp
try
{
    var result = await _mediator.Send(new GetUserQuery(999));
}
catch (HandlerNotFoundException ex)
{
    // Handler not found for request type
    _logger.LogError("Handler not found: {RequestType}, Expected: {HandlerType}",
        ex.RequestType, ex.ExpectedHandlerType);
}
catch (SymphonyException ex)
{
    // Base exception for all Symphony-related errors
    _logger.LogError("Symphony error [{ErrorCode}]: {Message}", ex.ErrorCode, ex.Message);

    // Access diagnostic context
    foreach (var (key, value) in ex.Context)
    {
        _logger.LogError("  {Key}: {Value}", key, value);
    }
}
```

## 🏗️ Architecture

```
├── Core/
│   ├── Mediator.cs                    # Main mediator implementation
│   └── MediatorExecutionHelper.cs     # Execution helper with Expression Trees
├── Interfaces/
│   ├── IMediator.cs                   # Main mediator interface
│   ├── IRequest.cs                    # Request marker interfaces
│   ├── IRequestHandler.cs             # Request handler interfaces
│   ├── IEventHandler.cs               # Event handler interface
│   ├── IPipelineBehavior.cs           # Pipeline behavior interface
│   └── Unit.cs                        # Unit type for void responses
├── Extensions/
│   ├── ServiceCollectionExtensions.cs # DI registration extensions
│   └── MediatorOptions.cs             # Fluent configuration options
└── Exceptions/
    └── SymphonyException.cs           # Custom exception hierarchy
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by [MediatR](https://github.com/jbogard/MediatR) with focus on performance optimization
- Built with modern .NET 8.0 features and best practices
- Thanks to all contributors and the .NET community

## 📞 Support

- 📧 Email: jarviisha@gmail.com
- 🐛 Issues: [GitHub Issues](https://github.com/jarviisha/LazyNet.Symphony/issues)
- 📖 Wiki: [Documentation](https://github.com/jarviisha/LazyNet.Symphony/wiki)

---

⭐ If you find this project useful, please give it a star on GitHub!
