using System.Text.Json.Serialization;

namespace AzureAgentRuntimeOrchestrator.Models;

[JsonSerializable(typeof(ExecutionStartRequest))]
[JsonSerializable(typeof(ExecutionDescriptor))]
[JsonSerializable(typeof(ExecutionState))]
internal partial class ExecutionJsonContext : JsonSerializerContext
{
}
