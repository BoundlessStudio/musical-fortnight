using Azure.Identity;
using AzureAgentRuntimeOrchestrator.Clients;
using AzureAgentRuntimeOrchestrator.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        builder.AddEnvironmentVariables();
    })
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.AddDurableTask();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient<IAzureSessionClient, AzureSessionClient>();
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
        services.AddOptions<AzureSessionOptions>()
            .Bind(context.Configuration.GetSection(AzureSessionOptions.SectionName))
            .ValidateDataAnnotations();
    })
    .Build();

await host.RunAsync();
