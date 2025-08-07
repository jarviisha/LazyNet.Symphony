using LazyNet.Symphony.Core;

namespace LazyNet.Symphony.Interfaces;

/// <summary>
/// Interface for pipeline behaviors that can process requests before and after the handler
/// </summary>
/// <typeparam name="TRequest">The type of request</typeparam>
/// <typeparam name="TResponse">The type of response</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request by wrapping the next behavior or handler
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="next">The next behavior in the pipeline or the final handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The response</returns>
    Task<TResponse> Handle(TRequest request, PipelineNext<TResponse> next, CancellationToken cancellationToken = default);
}
