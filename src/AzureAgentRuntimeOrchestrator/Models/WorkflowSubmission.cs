using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureAgentRuntimeOrchestrator.Models;

public record WorkflowSubmission(
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("workflowCode")] string WorkflowCode,
    [property: JsonPropertyName("input")] JsonElement? Input,
    [property: JsonPropertyName("runnerPreamble")] string? RunnerPreamble,
    [property: JsonPropertyName("environmentVariables")] IReadOnlyDictionary<string, string>? EnvironmentVariables,
    [property: JsonPropertyName("commandOverride")] string? CommandOverride);

[JsonSerializable(typeof(WorkflowSubmission))]
internal partial class WorkflowJsonContext : JsonSerializerContext
{
}
