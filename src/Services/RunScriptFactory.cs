using System.Text;

namespace MusicalFortnight.Services;

public static class RunScriptFactory
{
  public static string CreateRunnerScript(string workflowFileName, string inputFileName, string outputFileName, string? preamble)
  {
    var builder = new StringBuilder();
    builder.AppendLine("import json");
    builder.AppendLine("import importlib.util");
    builder.AppendLine("from pathlib import Path");
    builder.AppendLine();
    if (!string.IsNullOrWhiteSpace(preamble))
    {
      builder.AppendLine(preamble);
      builder.AppendLine();
    }

    builder.AppendLine("DATA_DIR = Path('/mnt/data')");
    builder.AppendLine($"WORKFLOW_PATH = DATA_DIR / '{workflowFileName}'");
    builder.AppendLine($"INPUT_PATH = DATA_DIR / '{inputFileName}'");
    builder.AppendLine($"OUTPUT_PATH = DATA_DIR / '{outputFileName}'");
    builder.AppendLine();
    builder.AppendLine("spec = importlib.util.spec_from_file_location('workflow', WORKFLOW_PATH)");
    builder.AppendLine("module = importlib.util.module_from_spec(spec)");
    builder.AppendLine("if spec is None or spec.loader is None:");
    builder.AppendLine("    raise ImportError('Unable to load workflow module')");
    builder.AppendLine("spec.loader.exec_module(module)  # type: ignore");
    builder.AppendLine();
    builder.AppendLine("with INPUT_PATH.open() as f:");
    builder.AppendLine("    payload = json.load(f)");
    builder.AppendLine();
    builder.AppendLine("if hasattr(module, 'run'):");
    builder.AppendLine("    result = module.run(payload)");
    builder.AppendLine("else:");
    builder.AppendLine("    raise AttributeError('workflow module must expose a run(payload) function')");
    builder.AppendLine();
    builder.AppendLine("with OUTPUT_PATH.open('w') as f:");
    builder.AppendLine("    json.dump(result, f)");
    builder.AppendLine();
    builder.AppendLine("print('Workflow execution completed. Results written to output.json')");

    return builder.ToString();
  }
}
