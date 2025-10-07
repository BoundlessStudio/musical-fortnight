using System.IO;
using System.Text;
using AzureAgentRuntimeOrchestrator.Clients;
using AzureAgentRuntimeOrchestrator.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureAgentRuntimeOrchestrator.Functions;

public class SessionActivities
{
    private readonly IAzureSessionClient _sessionClient;
    private readonly ILogger<SessionActivities> _logger;

    public SessionActivities(IAzureSessionClient sessionClient, ILogger<SessionActivities> logger)
    {
        _sessionClient = sessionClient;
        _logger = logger;
    }

    [Function("InitializeSession")]
    public Task<SessionDescriptor> InitializeSessionAsync([ActivityTrigger] SessionInitializationRequest request)
        => _sessionClient.EnsureSessionAsync(request);

    [Function("UploadArtifacts")]
    public async Task UploadArtifactsAsync([ActivityTrigger] UploadArtifactsRequest request)
    {
        await using var workflowStream = new MemoryStream(Encoding.UTF8.GetBytes(request.WorkflowCode));
        await _sessionClient.UploadFileAsync(request.Session.SessionId, request.WorkflowFileName, workflowStream, "text/x-python-script");

        await using var runnerStream = new MemoryStream(Encoding.UTF8.GetBytes(request.RunnerCode));
        await _sessionClient.UploadFileAsync(request.Session.SessionId, request.RunnerFileName, runnerStream, "text/x-python-script");

        await using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(request.InputJson));
        await _sessionClient.UploadFileAsync(request.Session.SessionId, request.InputFileName, inputStream, "application/json");
    }

    [Function("StartSessionExecution")]
    public Task<ExecutionDescriptor> StartSessionExecutionAsync([ActivityTrigger] ExecutionStartArguments arguments)
    {
        var request = new ExecutionStartRequest(arguments.Command, arguments.EnvironmentVariables);
        return _sessionClient.ExecuteCodeAsync(arguments.SessionId, request);
    }

    [Function("GetExecutionStatus")]
    public Task<ExecutionState> GetExecutionStatusAsync([ActivityTrigger] ExecutionStatusRequest request)
        => _sessionClient.GetExecutionStatusAsync(request.SessionId, request.ExecutionId);

    [Function("DownloadOutput")]
    public async Task<string> DownloadOutputAsync([ActivityTrigger] DownloadOutputRequest request)
    {
        await using var stream = await _sessionClient.DownloadFileAsync(request.SessionId, request.OutputFileName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        _logger.LogInformation("Downloaded {Length} bytes from output file", payload.Length);
        return payload;
    }
}
