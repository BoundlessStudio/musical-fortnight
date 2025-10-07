using System.ComponentModel.DataAnnotations;

namespace AzureAgentRuntimeOrchestrator.Options;

public class AzureSessionOptions
{
    public const string SectionName = "AzureSession";

    [Required]
    public string? BaseUrl { get; set; }

    [Required]
    public string? SessionPoolResourceId { get; set; }

    public string ApiVersion { get; set; } = "2024-10-02-preview";

    public int ExecutionPollIntervalSeconds { get; set; } = 10;

    public string WorkflowFileName { get; set; } = "workflow.py";

    public string RunnerFileName { get; set; } = "run.py";

    public string InputFileName { get; set; } = "input.json";

    public string OutputFileName { get; set; } = "output.json";

    public string PythonCommand { get; set; } = "python /mnt/data/run.py";
}
