# Symphony

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![NuGet](https://img.shields.io/nuget/v/LazyNet.Symphony.svg)](https://www.nuget.org/packages/LazyNet.Symphony/)

A high-performance, lightweight implementation of the Mediator pattern for .NET applications, providing a simple interface to handle requests, commands, queries, and domain events with advanced pipeline support.

> **⚠️ Alpha Release Warning**
> 
> This is an **alpha version (1.0.0-alpha)** of Symphony. While the core functionality is stable and tested, the API may still change in future releases. 
> 
> - ✅ Safe for **testing** and **evaluation**
> - ⚠️ **Not recommended** for production use yet
> - 📝 Breaking changes may occur before stable release
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

- **.NET 9.0** or later
- **Microsoft.Extensions.DependencyInjection** (included)
- Any **.NET 9.0 compatible** project (Web API, Console, etc.)

## ✨ Features

- **🚀 High Performance**: Uses compiled delegates and advanced caching for optimal performance
- **📨 Request/Response Pattern**: CQRS-style command and query handling
- **📡 Event Publishing**: Publish domain events to multiple handlers
- **🔧 Pipeline Behaviors**: Cross-cutting concerns like validation, logging, caching
- **💉 Dependency Injection**: Seamless integration with Microsoft.Extensions.DependencyInjection
- **🛡️ Exception Handling**: Detailed exception context and error information
- **📝 Full Documentation**: Comprehensive XML documentation and examples

## 📦 Installation

### Package Manager Console
```powershell
Install-Package LazyNet.Symphony
```

### .NET CLI
```bash
dotnet add package LazyNet.Symphony
```

### PackageReference
```xml
<PackageReference Include="LazyNet.Symphony" Version="1.0.0-alpha" />
```

## 🚀 Quick Start

### 1. Register Services

```csharp
using LazyNet.Symphony.Extensions;

// In Program.cs or Startup.cs
builder.Services.AddMediator(typeof(Program).Assembly);

// Or register from multiple assemblies
builder.Services.AddMediator(Assembly.GetExecutingAssembly());
```

### 2. Define Requests and Handlers

```csharp
using LazyNet.Symphony.Interfaces;

// Define a query
public record GetUserQuery(int UserId) : IRequest<User>;

// Define a command  
public record CreateUserCommand(string Name, string Email) : IRequest<int>;

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

// Command handler
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
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }
    
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Pre-processing: Validation
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(request, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();
        
        if (failures.Any())
        {
            throw new ValidationException(failures);
        }
        
        // Continue to next behavior or handler
        var response = await next();
        
        // Post-processing could be added here
        return response;
    }
}

// Register pipeline behaviors (order matters!)
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
```

## ⚡ Performance Features

Symphony is designed for high performance:

- **Compiled Delegates**: Uses expression compilation for fast handler invocation
- **Type Caching**: Caches generic types to avoid repeated reflection
- **Minimal Allocations**: Optimized memory usage with object pooling where possible
- **Concurrent Collections**: Thread-safe caching with `ConcurrentDictionary`

## 📚 Advanced Configuration

### Custom Mediator Options

```csharp
builder.Services.AddMediator(typeof(Program).Assembly, options =>
{
    options.EnablePerformanceLogging = true;
    options.DefaultTimeout = TimeSpan.FromSeconds(30);
    options.MaxConcurrentHandlers = Environment.ProcessorCount * 2;
});
```

### Manual Handler Registration

```csharp
// Register specific handlers manually
services.AddScoped<IRequestHandler<GetUserQuery, User>, GetUserQueryHandler>();
services.AddScoped<IEventHandler<UserCreatedEvent>, UserCreatedEventHandler>();
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
    _logger.LogError("Handler not found: {RequestType}", ex.RequestType);
}
catch (SymphonyException ex)
{
    // Base exception for all Symphony-related errors
    _logger.LogError("Symphony error: {Message}, Context: {Context}", ex.Message, ex.Context);
}
```

## 🏗️ Architecture

```
├── Core/
│   ├── Mediator.cs                    # Main mediator implementation
│   └── MediatorExecutionHelper.cs     # Execution helper utilities
├── Interfaces/
│   ├── IMediator.cs                   # Main mediator interface
│   ├── IRequest.cs                    # Request marker interface
│   ├── IRequestHandler.cs             # Request handler interface
│   ├── IEventHandler.cs               # Event handler interface
│   └── IPipelineBehavior.cs           # Pipeline behavior interface
├── Extensions/
│   ├── ServiceCollectionExtensions.cs # DI registration extensions
│   └── MediatorOptions.cs             # Configuration options
└── Exceptions/
    └── SymphonyException.cs           # Custom exceptions
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
- Built with modern .NET 9.0 features and best practices
- Thanks to all contributors and the .NET community

## 📞 Support

- 📧 Email: jarviisha@gmail.com
- 🐛 Issues: [GitHub Issues](https://github.com/jarviisha/LazyNet.Symphony/issues)
- 📖 Wiki: [Documentation](https://github.com/jarviisha/LazyNet.Symphony/wiki)

---

⭐ If you find this project useful, please give it a star on GitHub!