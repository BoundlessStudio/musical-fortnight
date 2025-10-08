

namespace MusicalFortnight.Models;

public record ExecutionStartArguments(string SessionId, string Command, IReadOnlyDictionary<string, string>? EnvironmentVariables);
