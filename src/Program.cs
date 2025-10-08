using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MusicalFortnight.Clients;
using MusicalFortnight.Configuration;
using System.Reflection;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables();

builder.ConfigureFunctionsWebApplication();

// strongly-typed options
builder.Services.AddOptions<AzureSessionOptions>()
    .Bind(builder.Configuration.GetSection(AzureSessionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.ConfigureDurableExtension();
builder.Services.AddHttpClient<IAzureSessionClient, AzureSessionClient>();
builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();


builder.Build().Run();
