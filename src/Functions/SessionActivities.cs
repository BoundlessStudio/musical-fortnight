using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MusicalFortnight.Clients;
using MusicalFortnight.Models;
using System.Text;

namespace MusicalFortnight.Functions;

public class SessionActivities
{
  private readonly IAzureSessionClient _sessionClient;

  public SessionActivities(IAzureSessionClient sessionClient)
  {
    this._sessionClient = sessionClient;
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
  public async Task<string> DownloadOutputAsync([ActivityTrigger] DownloadOutputRequest request, ILogger logger)
  {
    await using var stream = await _sessionClient.DownloadFileAsync(request.SessionId, request.OutputFileName);
    using var reader = new StreamReader(stream);
    var payload = await reader.ReadToEndAsync();
    logger.LogInformation("Downloaded {Length} bytes from output file", payload.Length);
    return payload;
  }
}
