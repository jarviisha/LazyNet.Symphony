using LazyNet.Symphony.Core;

namespace LazyNet.Symphony.Interfaces;

/// <summary>
/// Interface for handling requests that return a response.
/// </summary>
/// <typeparam name="TRequest">The type of request to handle.</typeparam>
/// <typeparam name="TResponse">The type of response to return.</typeparam>
/// <remarks>
/// Implement this interface for query handlers that need to return data
/// (e.g., GetUserByIdQuery, SearchProductsQuery).
/// </remarks>
/// <example>
/// <code>
/// public class GetUserByIdHandler : IRequestHandler&lt;GetUserByIdQuery, User&gt;
/// {
///     public async Task&lt;User&gt; Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
///     {
///         return await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
///     }
/// }
/// </code>
/// </example>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request and returns a response.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for handling requests that don't return a meaningful value (void commands).
/// This is syntactic sugar for <see cref="IRequestHandler{TRequest, Unit}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <remarks>
/// <para>
/// Implement this interface for command handlers that perform an action
/// but don't need to return data (e.g., CreateUserCommand, SendEmailCommand).
/// </para>
/// <para>
/// <strong>IMPORTANT:</strong> When implementing this interface, you ONLY need to implement
/// the <see cref="HandleAsync"/> method. The framework automatically provides a default
/// implementation of <see cref="IRequestHandler{TRequest, Unit}.Handle"/> that calls
/// <see cref="HandleAsync"/> and wraps the result in <see cref="Unit"/>.
/// </para>
/// <para>
/// Do NOT manually implement the <see cref="IRequestHandler{TRequest, Unit}.Handle"/> method
/// as it uses explicit interface implementation to prevent accidental overrides.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Correct implementation - only implement HandleAsync
/// public class CreateUserHandler : IRequestHandler&lt;CreateUserCommand&gt;
/// {
///     public async Task HandleAsync(CreateUserCommand request, CancellationToken cancellationToken)
///     {
///         // Your command logic here
///         await _userRepository.CreateAsync(request.User, cancellationToken);
///     }
/// }
/// </code>
/// </example>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
    /// <summary>
    /// Handles the request without returning a value.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This is the only method you need to implement. The base <see cref="IRequestHandler{TRequest, Unit}.Handle"/>
    /// method is automatically implemented to call this method and return <see cref="Unit.Value"/>.
    /// </remarks>
    Task HandleAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Default implementation that wraps <see cref="HandleAsync"/> and returns <see cref="Unit.Value"/>.
    /// </summary>
    /// <remarks>
    /// This method uses explicit interface implementation to prevent accidental overrides.
    /// You should NOT implement this method manually - implement <see cref="HandleAsync"/> instead.
    /// </remarks>
    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await HandleAsync(request, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
