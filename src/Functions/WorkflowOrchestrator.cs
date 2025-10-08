using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using MusicalFortnight.Models;

namespace MusicalFortnight.Functions;

public class WorkflowOrchestrator
{
  public WorkflowOrchestrator()
  {
  }

  [Function("WorkflowOrchestrator")]
  public async Task<WorkflowOrchestrationResult> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
  {
    ILogger logger = context.CreateReplaySafeLogger(nameof(WorkflowFunction));
    logger.LogInformation("Saying hello.");
    var outputs = new List<string>();

    var input = context.GetInput<WorkflowOrchestrationInput>() ?? throw new InvalidOperationException("Orchestration input is required.");

    var session = await context.CallActivityAsync<SessionDescriptor>("InitializeSession", new SessionInitializationRequest(input.SessionId));

    await context.CallActivityAsync("UploadArtifacts", new UploadArtifactsRequest(
        session,
        input.WorkflowCode,
        input.RunnerCode,
        input.InputJson,
        input.WorkflowFileName,
        input.RunnerFileName,
        input.InputFileName));

    var execution = await context.CallActivityAsync<ExecutionDescriptor>("StartSessionExecution", new ExecutionStartArguments(session.SessionId, input.Command, input.EnvironmentVariables));

    ExecutionState state;
    do
    {
      var nextCheck = context.CurrentUtcDateTime.AddSeconds(input.PollIntervalSeconds);
      await context.CreateTimer(nextCheck, CancellationToken.None);
      state = await context.CallActivityAsync<ExecutionState>("GetExecutionStatus", new ExecutionStatusRequest(session.SessionId, execution.Id));
      logger.LogInformation("Execution {ExecutionId} status {Status}", execution.Id, state.Status);
    } while (!IsTerminal(state.Status));

    if (!IsSuccessful(state.Status))
    {
      throw new InvalidOperationException($"Execution {execution.Id} did not succeed. Status: {state.Status}");
    }

    var outputJson = await context.CallActivityAsync<string>("DownloadOutput", new DownloadOutputRequest(session.SessionId, input.OutputFileName));

    return new WorkflowOrchestrationResult(session.SessionId, outputJson);
  }


  private static bool IsTerminal(string status)
      => string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
         || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
         || string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
         || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

  private static bool IsSuccessful(string status)
      => string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase)
         || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
}
