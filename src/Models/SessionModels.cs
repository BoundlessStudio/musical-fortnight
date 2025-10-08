using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicalFortnight.Models;

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
