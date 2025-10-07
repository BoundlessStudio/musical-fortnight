using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzureAgentRuntimeOrchestrator.Models;

public record SessionInitializationRequest(string? SessionId, string? PreferredSessionId = null, int? ExpirationSeconds = null, int? MaxExecutions = null);

public record SessionDescriptor(string SessionId, bool Created);

public record UploadArtifactsRequest(
    SessionDescriptor Session,
    string WorkflowCode,
    string RunnerCode,
    string InputJson,
    string WorkflowFileName,
    string RunnerFileName,
    string InputFileName);

public record ExecutionStartRequest(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("environmentVariables")] IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    [property: JsonPropertyName("workingDirectory")] string? WorkingDirectory = null);

public record ExecutionDescriptor(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status);

public record ExecutionState(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("error")] JsonElement? Error,
    [property: JsonPropertyName("completedOn")] DateTimeOffset? CompletedOn);

public record ExecutionStatusRequest(string SessionId, string ExecutionId);

public record DownloadOutputRequest(string SessionId, string OutputFileName);

public record WorkflowOrchestrationInput(
    string? SessionId,
    string WorkflowCode,
    string RunnerCode,
    string InputJson,
    int PollIntervalSeconds,
    string WorkflowFileName,
    string RunnerFileName,
    string InputFileName,
    string OutputFileName,
    string Command,
    IReadOnlyDictionary<string, string>? EnvironmentVariables);

public record WorkflowOrchestrationResult(string SessionId, string OutputJson);
