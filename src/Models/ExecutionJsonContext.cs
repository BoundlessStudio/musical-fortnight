using System.Text.Json.Serialization;

namespace MusicalFortnight.Models;

[JsonSerializable(typeof(ExecutionStartRequest))]
[JsonSerializable(typeof(ExecutionDescriptor))]
[JsonSerializable(typeof(ExecutionState))]
internal partial class ExecutionJsonContext : JsonSerializerContext
{
}
