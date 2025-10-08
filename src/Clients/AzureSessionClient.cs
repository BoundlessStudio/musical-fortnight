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
  Task UploadFileAsync(string sessionId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
  Task<ExecutionDescriptor> ExecuteCodeAsync(string sessionId, ExecutionStartRequest request, CancellationToken cancellationToken = default);
  Task<Stream> DownloadFileAsync(string sessionId, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Updated client for API version 2024-10-02-preview and later.
/// Endpoints:
///   POST   /executions?api-version=...&identifier=...
///   GET    /executions/{id}?api-version=...&identifier=...
///   POST   /files?api-version=...&identifier=...&path=/mnt/data   (multipart, field name "file")
///   GET    /files/{name}/content?api-version=...&identifier=...
/// </summary>
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

  public async Task UploadFileAsync(string sessionId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
  {
    // POST /files?api-version=...&identifier=...&path=/mnt/data
    var path = $"files{BuildQuery(sessionId, extra: "path=/mnt/data")}";
    using var form = new MultipartFormDataContent();

    var fileContent = new StreamContent(content);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
    form.Add(fileContent, "file", fileName);

    using var response = await SendAsync(HttpMethod.Post, path, form, cancellationToken);
    response.EnsureSuccessStatusCode();
  }

  public async Task<ExecutionDescriptor> ExecuteCodeAsync(string sessionId, ExecutionStartRequest request, CancellationToken cancellationToken = default)
  {
    // POST /executions?api-version=...&identifier=...
    var path = $"executions{BuildQuery(sessionId)}";

    // Map your model to the new wire shape if needed.
    // Expect fields like: codeInputType, executionType, code, timeoutInSeconds, identifier optional redundantly.
    var payload = JsonSerializer.Serialize(request, ExecutionJsonContext.Default.ExecutionStartRequest);
    using var content = new StringContent(payload, Encoding.UTF8, "application/json");

    using var response = await SendAsync(HttpMethod.Post, path, content, cancellationToken);
    response.EnsureSuccessStatusCode();

    await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
    var descriptor = await JsonSerializer.DeserializeAsync(body, ExecutionJsonContext.Default.ExecutionDescriptor, cancellationToken: cancellationToken);
    if (descriptor is null) throw new InvalidOperationException("Unable to deserialize execution descriptor.");
    return descriptor;
  }

  public async Task<Stream> DownloadFileAsync(string sessionId, string fileName, CancellationToken cancellationToken = default)
  {
    // GET /files/{name}/content?api-version=...&identifier=...
    var path = $"files/{Uri.EscapeDataString(fileName)}/content{BuildQuery(sessionId)}";
    using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
    response.EnsureSuccessStatusCode();

    var memory = new MemoryStream();
    await response.Content.CopyToAsync(memory, cancellationToken);
    memory.Position = 0;
    return memory;
  }

  private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, HttpContent? content, CancellationToken cancellationToken)
  {
    var baseUri = new Uri(_options.BaseUrl!, UriKind.Absolute);
    var request = new HttpRequestMessage(method, new Uri(baseUri, relativePath));
    if (content is not null) request.Content = content;

    // Only set Authorization if a credential was provided.
    if (_credential is not null)
    {
      var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://dynamicsessions.io/.default" }), cancellationToken);
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    _logger.LogDebug("HTTP {Method} {Uri}", method, request.RequestUri);
    return await _httpClient.SendAsync(request, cancellationToken);
  }

  private string BuildQuery(string identifier, string? extra = null)
  {
    var sb = new StringBuilder();
    sb.Append($"?api-version={Uri.EscapeDataString(_options.ApiVersion!)}");
    sb.Append($"&identifier={Uri.EscapeDataString(identifier)}");
    if (!string.IsNullOrWhiteSpace(extra))
    {
      sb.Append('&');
      sb.Append(extra);
    }
    return sb.ToString();
  }
}
