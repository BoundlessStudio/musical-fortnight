using AzureAgentRuntimeOrchestrator.Models;

namespace AzureAgentRuntimeOrchestrator.Clients;

public interface IAzureSessionClient
{
    Task<SessionDescriptor> EnsureSessionAsync(SessionInitializationRequest request, CancellationToken cancellationToken = default);

    Task UploadFileAsync(string sessionId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<ExecutionDescriptor> ExecuteCodeAsync(string sessionId, ExecutionStartRequest request, CancellationToken cancellationToken = default);

    Task<ExecutionState> GetExecutionStatusAsync(string sessionId, string executionId, CancellationToken cancellationToken = default);

    Task<Stream> DownloadFileAsync(string sessionId, string fileName, CancellationToken cancellationToken = default);
}
