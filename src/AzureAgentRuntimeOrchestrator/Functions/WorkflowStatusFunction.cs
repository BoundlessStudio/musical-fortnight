using System.Net;
using System.Text.Json;
using AzureAgentRuntimeOrchestrator.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace AzureAgentRuntimeOrchestrator.Functions;

public class WorkflowStatusFunction
{
    private readonly IDurableClientFactory _durableClientFactory;
    private readonly ILogger<WorkflowStatusFunction> _logger;

    public WorkflowStatusFunction(IDurableClientFactory durableClientFactory, ILogger<WorkflowStatusFunction> logger)
    {
        _durableClientFactory = durableClientFactory;
        _logger = logger;
    }

    [Function("GetWorkflowStatus")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", Route = "workflowStatus/{instanceId}")] HttpRequestData req, string instanceId)
    {
        var client = _durableClientFactory.CreateClient();
        var status = await client.GetInstanceAsync(instanceId);
        if (status is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Instance not found." });
            return notFound;
        }

        _logger.LogInformation("Retrieved status for {InstanceId}: {RuntimeStatus}", status.InstanceId, status.RuntimeStatus);

        WorkflowOrchestrationResult? output = null;
        try
        {
            output = status.ReadOutputAs<WorkflowOrchestrationResult>();
        }
        catch (InvalidOperationException)
        {
            _logger.LogDebug("Workflow {InstanceId} output not yet available.", instanceId);
        }

        JsonElement? customStatus = null;
        try
        {
            customStatus = status.ReadCustomStatusAs<JsonElement?>();
        }
        catch (InvalidOperationException)
        {
            _logger.LogDebug("Workflow {InstanceId} custom status not set.", instanceId);
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
