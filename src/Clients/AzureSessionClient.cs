using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicalFortnight.Configuration;
using MusicalFortnight.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MusicalFortnight.Clients;

public interface IAzureSessionClient
{
  Task<SessionDescriptor> EnsureSessionAsync(SessionInitializationRequest request, CancellationToken cancellationToken = default);

  Task UploadFileAsync(string sessionId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

  Task<ExecutionDescriptor> ExecuteCodeAsync(string sessionId, ExecutionStartRequest request, CancellationToken cancellationToken = default);

  Task<ExecutionState> GetExecutionStatusAsync(string sessionId, string executionId, CancellationToken cancellationToken = default);

  Task<Stream> DownloadFileAsync(string sessionId, string fileName, CancellationToken cancellationToken = default);
}


public class AzureSessionClient : IAzureSessionClient
{
  private readonly HttpClient _httpClient;
  private readonly TokenCredential _credential;
  private readonly AzureSessionOptions _options;
  private readonly ILogger<AzureSessionClient> _logger;

  public AzureSessionClient(HttpClient httpClient, TokenCredential credential, IOptions<AzureSessionOptions> options, ILogger<AzureSessionClient> logger)
  {
    _httpClient = httpClient;
    _credential = credential;
    _options = options.Value;
    _logger = logger;
  }

  public async Task<SessionDescriptor> EnsureSessionAsync(SessionInitializationRequest request, CancellationToken cancellationToken = default)
  {
    if (!string.IsNullOrWhiteSpace(request.SessionId))
    {
      return new SessionDescriptor(request.SessionId!, false);
    }

    var payload = new
    {
      properties = new
      {
        sessionId = request.PreferredSessionId ?? Guid.NewGuid().ToString("N"),
        expirationSeconds = request.ExpirationSeconds,
        maxExecutions = request.MaxExecutions
      }
    };

    var path = $"{_options.SessionPoolResourceId}/sessions?api-version={_options.ApiVersion}";
    using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    using var response = await SendAsync(HttpMethod.Post, path, content, cancellationToken);
    response.EnsureSuccessStatusCode();

    using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
    var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
    string? sessionId = null;
    if (document.RootElement.TryGetProperty("properties", out var properties)
        && properties.TryGetProperty("sessionId", out var sessionIdProperty))
    {
      sessionId = sessionIdProperty.GetString();
    }

    if (string.IsNullOrEmpty(sessionId) && document.RootElement.TryGetProperty("name", out var nameProperty))
    {
      sessionId = nameProperty.GetString();
    }

    if (string.IsNullOrEmpty(sessionId))
    {
      throw new InvalidOperationException("Session ID missing from session creation response.");
    }

    return new SessionDescriptor(sessionId!, true);
  }

  public async Task UploadFileAsync(string sessionId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    var path = BuildSessionPath(sessionId, $"files/{Uri.EscapeDataString(fileName)}");
    using var requestContent = new StreamContent(content);
    requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

    using var response = await SendAsync(HttpMethod.Post, path, requestContent, cancellationToken);
    response.EnsureSuccessStatusCode();
  }

  public async Task<ExecutionDescriptor> ExecuteCodeAsync(string sessionId, ExecutionStartRequest request, CancellationToken cancellationToken = default)
  {
    var path = BuildSessionPath(sessionId, "executions");
    var payload = JsonSerializer.Serialize(request, ExecutionJsonContext.Default.ExecutionStartRequest);
    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
    using var response = await SendAsync(HttpMethod.Post, path, content, cancellationToken);
    response.EnsureSuccessStatusCode();

    using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
    var descriptor = await JsonSerializer.DeserializeAsync(body, ExecutionJsonContext.Default.ExecutionDescriptor, cancellationToken: cancellationToken);
    if (descriptor is null)
    {
      throw new InvalidOperationException("Unable to deserialize execution descriptor.");
    }

    return descriptor;
  }

  public async Task<ExecutionState> GetExecutionStatusAsync(string sessionId, string executionId, CancellationToken cancellationToken = default)
  {
    var path = BuildSessionPath(sessionId, $"executions/{Uri.EscapeDataString(executionId)}");
    using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
    response.EnsureSuccessStatusCode();

    using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
    var status = await JsonSerializer.DeserializeAsync(body, ExecutionJsonContext.Default.ExecutionState, cancellationToken: cancellationToken);
    if (status is null)
    {
      throw new InvalidOperationException("Unable to deserialize execution status.");
    }

    return status;
  }

  public async Task<Stream> DownloadFileAsync(string sessionId, string fileName, CancellationToken cancellationToken = default)
  {
    var path = BuildSessionPath(sessionId, $"files/{Uri.EscapeDataString(fileName)}/content");
    using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
    response.EnsureSuccessStatusCode();

    var memory = new MemoryStream();
    await response.Content.CopyToAsync(memory, cancellationToken);
    memory.Position = 0;
    return memory;
  }

  private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, HttpContent? content, CancellationToken cancellationToken)
  {
    var request = new HttpRequestMessage(method, new Uri(new Uri(_options.BaseUrl!, UriKind.Absolute), relativePath));
    if (content is not null)
    {
      request.Content = content;
    }

    var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://dynamicsessions.io/.default" }), cancellationToken);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

    _logger.LogDebug("Sending {Method} request to {Uri}", method, request.RequestUri);

    return await _httpClient.SendAsync(request, cancellationToken);
  }

  private string BuildSessionPath(string sessionId, string resource)
      => $"{_options.SessionPoolResourceId}/sessions/{Uri.EscapeDataString(sessionId)}/{resource}?api-version={_options.ApiVersion}";
}
