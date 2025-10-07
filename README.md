# Azure Agent Runtime Orchestrator

This repository contains the scaffolding for an Azure Functions (Durable Functions) project that orchestrates execution of exported OpenAI Agent Builder workflows inside Azure Container Apps Dynamic Session Pools.

## Solution Overview

The Durable Function accepts workflow Python code and JSON input, stores the artifacts inside a Dynamic Session, triggers asynchronous execution using Azure's 2024-10-02-preview Session Pools API, and polls until results are available. Once execution is successful the orchestrator downloads `output.json` and returns the serialized payload to the caller.

### Key Components

- **HTTP starter (`StartWorkflow`)** – accepts workflow submissions and starts the orchestration.
- **Durable orchestrator (`WorkflowOrchestrator`)** – coordinates session creation, file uploads, execution, polling, and output retrieval.
- **Activity functions (`SessionActivities`)** – wrap calls to the `AzureSessionClient` for each REST API operation.
- **`AzureSessionClient`** – reusable client that authenticates with managed identity, uploads artifacts, triggers executions, and retrieves results from the Dynamic Session Pools API.

### HTTP Endpoints

- `POST /api/workflows` – submit a workflow. Body fields:
  - `workflowCode` *(string, required)* – Python module containing a `run(payload)` function.
  - `input` *(object, optional)* – arbitrary JSON payload made available as `input.json`.
  - `sessionId` *(string, optional)* – reuse an existing Dynamic Session.
  - `environmentVariables` *(object, optional)* – injected into the execution environment.
  - `runnerPreamble` *(string, optional)* – additional Python placed at the top of `run.py`.
  - `commandOverride` *(string, optional)* – overrides the default `python /mnt/data/run.py` command.
- `GET /api/workflowStatus/{instanceId}` – retrieve Durable Function runtime status and (if complete) the serialized workflow output.

### Configuration

Runtime configuration is provided through the `AzureSession` section in `appsettings.json` or equivalent environment variables:

```json
{
  "AzureSession": {
    "BaseUrl": "https://<region>.dynamicsessions.azure.com",
    "SessionPoolResourceId": "/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.App/sessionPools/<session-pool-name>",
    "ExecutionPollIntervalSeconds": 10,
    "WorkflowFileName": "workflow.py",
    "RunnerFileName": "run.py",
    "InputFileName": "input.json",
    "OutputFileName": "output.json",
    "PythonCommand": "python /mnt/data/run.py"
  }
}
```

Additional settings (e.g., storage connection, logging) should be configured according to your Azure Functions hosting environment.

### Building Locally

> **Note:** The development container used to author this scaffold does not include the .NET SDK, so the project has not been built or executed here. Install the .NET 8 SDK and the Azure Functions Core Tools locally to run or debug the function app.

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Install [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local).
3. From the `src/AzureAgentRuntimeOrchestrator` directory, restore and run:

    ```bash
    dotnet restore
    func start
    ```

### Next Steps

- Implement richer validation for workflow submissions and result handling.
- Extend the `AzureSessionClient` with session pooling strategies (reuse vs. create).
- Add integration tests once the Azure Dynamic Sessions emulator or staging environment is available.
