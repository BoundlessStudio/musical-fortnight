using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicalFortnight.Configuration;
using MusicalFortnight.Models;
using MusicalFortnight.Services;
using System.Net;
using System.Text.Json;
using System.Threading;

namespace MusicalFortnight.Functions;

public class WorkflowFunction
{
  private readonly AzureSessionOptions options;

  public WorkflowFunction(IOptions<AzureSessionOptions> options)
  {
    this.options = options.Value;
  }

  [Function("WorkflowStart")]
  public async Task<HttpResponseData> WorkflowRun(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workflow")] HttpRequestData req,
      [DurableClient] DurableTaskClient client,
      FunctionContext executionContext)
  {
    ILogger logger = executionContext.GetLogger("WorkflowStart");

    var body = await req.ReadFromJsonAsync<WorkflowSubmission>();
    if (body is null)
    {
      var response = req.CreateResponse(HttpStatusCode.BadRequest);
      await response.WriteAsJsonAsync(new { error = "Request body is required." });
      return response;
    }

    var runner = RunScriptFactory.CreateRunnerScript(
      options.WorkflowFileName,
      options.InputFileName, 
      options.OutputFileName, 
      body.RunnerPreamble
    );

    var input = new WorkflowOrchestrationInput(
      body.SessionId,
      body.WorkflowCode,
      runner,
      body.Input?.GetRawText() ?? "{}",
      options.ExecutionPollIntervalSeconds,
      options.WorkflowFileName,
      options.RunnerFileName,
      options.InputFileName,
      options.OutputFileName,
      body.CommandOverride ?? options.PythonCommand,
      body.EnvironmentVariables
    );

    // Function input comes from the request content.
    string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("WorkflowOrchestrator", input);

    logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

    // Returns an HTTP 202 response with an instance management payload.
    // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
    return await client.CreateCheckStatusResponseAsync(req, instanceId);
  }


  [Function("WorkflowStatus")]
  public async Task<HttpResponseData> RunAsync(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "workflow/{instanceId}")] HttpRequestData req, string instanceId,
    [DurableClient] DurableTaskClient client,
    FunctionContext executionContext)
  {
    ILogger logger = executionContext.GetLogger("WorkflowStart");

    var status = await client.GetInstanceAsync(instanceId);
    if (status is null)
    {
      var notFound = req.CreateResponse(HttpStatusCode.NotFound);
      await notFound.WriteAsJsonAsync(new { error = "Instance not found." });
      return notFound;
    }

   logger.LogInformation("Retrieved status for {InstanceId}: {RuntimeStatus}", status.InstanceId, status.RuntimeStatus);

    WorkflowOrchestrationResult? output = null;
    try
    {
      output = status.ReadOutputAs<WorkflowOrchestrationResult>();
    }
    catch (InvalidOperationException)
    {
      logger.LogDebug("Workflow {InstanceId} output not yet available.", instanceId);
    }

    JsonElement? customStatus = null;
    try
    {
      customStatus = status.ReadCustomStatusAs<JsonElement?>();
    }
    catch (InvalidOperationException)
    {
      logger.LogDebug("Workflow {InstanceId} custom status not set.", instanceId);
    }

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new
    {
      instanceId = status.InstanceId,
      runtimeStatus = status.RuntimeStatus.ToString(),
      output,
      customStatus
    });

    return response;
  }
}