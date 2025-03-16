using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace Reported.External;


public sealed class AxiomHttpClient: IHttpClient
{
    private readonly HttpClient _httpClient;

    public AxiomHttpClient(string apiToken)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", apiToken);
    }

    public void Configure(IConfiguration configuration)
    {
    }

    public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream, CancellationToken cancellationToken = default)
    {
        var content = new StreamContent(contentStream);
        content.Headers.Add("Content-Type", "application/json");
        return await _httpClient.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
