using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicalFortnight.Models;

// New: identifier is the handle used in querystring.
public record SessionDescriptor(
  [property: JsonPropertyName("identifier")] string Identifier,
  bool Created
);


// Request body for POST /executions?api-version=...&identifier=...
public record ExecutionStartRequest(
  // "inline" or "files" (spec uses lowercase)
  [property: JsonPropertyName("codeInputType")] string CodeInputType,
  // "Synchronous" or "Asynchronous" (spec uses PascalCase)
  [property: JsonPropertyName("executionType")] string ExecutionType,
  // Required when codeInputType == "inline"
  [property: JsonPropertyName("code")] string? Code = null,
  [property: JsonPropertyName("timeoutInSeconds")] int? TimeoutInSeconds = null
);

// Unified shape for both the 202 Accepted (body) and the later GET poll result.
public record ExecutionDescriptor(
  [property: JsonPropertyName("id")] string Id,
  [property: JsonPropertyName("identifier")] string Identifier,
  [property: JsonPropertyName("sessionId")] string SessionId,
  [property: JsonPropertyName("executionType")] string ExecutionType,
  [property: JsonPropertyName("status")] string Status,
  [property: JsonPropertyName("result")] ExecutionResult? Result,
  // Raw payload varies; keep flexible
  [property: JsonPropertyName("rawResult")] JsonElement? RawResult
);

// Alias for status polling if you prefer a distinct type.
public record ExecutionState(
  [property: JsonPropertyName("id")] string Id,
  [property: JsonPropertyName("identifier")] string Identifier,
  [property: JsonPropertyName("sessionId")] string SessionId,
  [property: JsonPropertyName("executionType")] string ExecutionType,
  [property: JsonPropertyName("status")] string Status,
  [property: JsonPropertyName("result")] ExecutionResult? Result,
  [property: JsonPropertyName("rawResult")] JsonElement? RawResult
);

// Matches "result" block in succeeded responses.
public record ExecutionResult(
  [property: JsonPropertyName("stdout")] string? Stdout,
  [property: JsonPropertyName("stderr")] string? Stderr,
  [property: JsonPropertyName("executionResult")] string? ExecutionResultText,
  [property: JsonPropertyName("executionTimeInMilliseconds")] int? ExecutionTimeInMilliseconds
);

// File metadata returned by /files endpoints.
public record SessionFile(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("sizeInBytes")] long SizeInBytes,
  [property: JsonPropertyName("lastModifiedAt")] DateTimeOffset LastModifiedAt,
  [property: JsonPropertyName("contentType")] string ContentType,
  // e.g., "File"
  [property: JsonPropertyName("type")] string Type
);

// Wrapper for GET /files listing.
public record SessionFileList(
  [property: JsonPropertyName("value")] IReadOnlyList<SessionFile> Value
);

// Optional: System.Text.Json source-gen context.
[JsonSerializable(typeof(ExecutionStartRequest))]
[JsonSerializable(typeof(ExecutionDescriptor))]
[JsonSerializable(typeof(ExecutionState))]
[JsonSerializable(typeof(ExecutionResult))]
[JsonSerializable(typeof(SessionFile))]
[JsonSerializable(typeof(SessionFileList))]
public partial class ExecutionJsonContext : JsonSerializerContext { }