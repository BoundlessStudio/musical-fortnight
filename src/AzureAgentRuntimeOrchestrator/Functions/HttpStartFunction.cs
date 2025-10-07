using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using AzureAgentRuntimeOrchestrator.Models;
using AzureAgentRuntimeOrchestrator.Options;
using AzureAgentRuntimeOrchestrator.Utilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureAgentRuntimeOrchestrator.Functions;

public class HttpStartFunction
{
    private readonly IDurableClientFactory _durableClientFactory;
    private readonly AzureSessionOptions _options;
    private readonly ILogger<HttpStartFunction> _logger;

    public HttpStartFunction(IDurableClientFactory durableClientFactory, IOptions<AzureSessionOptions> options, ILogger<HttpStartFunction> logger)
    {
        _durableClientFactory = durableClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    [Function("StartWorkflow")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = "workflows")] HttpRequestData req)
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        var requestBody = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return await CreateBadRequestAsync(req, "Request body is required.");
        }

        WorkflowSubmission? submission;
        try
        {
            submission = JsonSerializer.Deserialize(requestBody, WorkflowJsonContext.Default.WorkflowSubmission);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse workflow submission.");
            return await CreateBadRequestAsync(req, "Invalid JSON payload.");
        }

        if (submission is null || string.IsNullOrWhiteSpace(submission.WorkflowCode))
        {
            return await CreateBadRequestAsync(req, "workflowCode is required.");
        }

        var input = new WorkflowOrchestrationInput(
            submission.SessionId,
            submission.WorkflowCode,
            RunScriptFactory.CreateRunnerScript(_options.WorkflowFileName, _options.InputFileName, _options.OutputFileName, submission.RunnerPreamble),
            submission.Input?.GetRawText() ?? "{}",
            _options.ExecutionPollIntervalSeconds,
            _options.WorkflowFileName,
            _options.RunnerFileName,
            _options.InputFileName,
            _options.OutputFileName,
            submission.CommandOverride ?? _options.PythonCommand,
            submission.EnvironmentVariables);

        var client = _durableClientFactory.CreateClient();
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("WorkflowOrchestrator", input);

        _logger.LogInformation("Started orchestration with ID {InstanceId}", instanceId);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new
        {
            instanceId,
            sessionId = submission.SessionId
        });

        return response;
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}
