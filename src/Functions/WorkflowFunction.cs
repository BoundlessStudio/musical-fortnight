using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicalFortnight.Configuration;
using MusicalFortnight.Models;
using MusicalFortnight.Services;
using System.Net;

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
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workflows")] HttpRequestData req,
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
}