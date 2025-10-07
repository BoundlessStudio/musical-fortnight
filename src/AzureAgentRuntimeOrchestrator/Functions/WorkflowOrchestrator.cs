using System.Collections.Generic;
using System.Threading;
using AzureAgentRuntimeOrchestrator.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace AzureAgentRuntimeOrchestrator.Functions;

public class WorkflowOrchestrator
{
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public WorkflowOrchestrator(ILogger<WorkflowOrchestrator> logger)
    {
        _logger = logger;
    }

    [Function("WorkflowOrchestrator")]
    public async Task<WorkflowOrchestrationResult> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
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
            _logger.LogInformation("Execution {ExecutionId} status {Status}", execution.Id, state.Status);
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

public record ExecutionStartArguments(string SessionId, string Command, IReadOnlyDictionary<string, string>? EnvironmentVariables);
