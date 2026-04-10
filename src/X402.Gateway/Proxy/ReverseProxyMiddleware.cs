using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using X402.Gateway.Config;

namespace X402.Gateway.Proxy;

public sealed class ReverseProxyMiddleware
{
    private readonly X402GatewayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReverseProxyMiddleware> _logger;

    public ReverseProxyMiddleware(
        RequestDelegate _,
        X402GatewayConfig config,
        HttpClient httpClient,
        ILogger<ReverseProxyMiddleware> logger)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrEmpty(_config.ProxyBaseUrl))
        {
            _logger.LogError("ProxyBaseUrl is not configured");
            context.Response.StatusCode = 502;
            await context.Response.WriteAsJsonAsync(new { error = "Proxy not configured" });
            return;
        }

        var targetUrl = $"{_config.ProxyBaseUrl.TrimEnd('/')}{context.Request.Path}{context.Request.QueryString}";

        using var proxyRequest = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            targetUrl);

        // Copy request body
        if (context.Request.ContentLength > 0 || context.Request.ContentType is not null)
        {
            proxyRequest.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType is not null)
                proxyRequest.Content.Headers.ContentType =
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
        }

        // Inject merchant auth header
        if (!string.IsNullOrEmpty(_config.ProxyAuthHeader) && !string.IsNullOrEmpty(_config.ProxyAuthValue))
        {
            proxyRequest.Headers.TryAddWithoutValidation(_config.ProxyAuthHeader, _config.ProxyAuthValue);
        }

        // Forward select headers
        foreach (var header in context.Request.Headers)
        {
            if (ShouldForwardHeader(header.Key))
                proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        try
        {
            using var proxyResponse = await _httpClient.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead);

            context.Response.StatusCode = (int)proxyResponse.StatusCode;

            foreach (var header in proxyResponse.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in proxyResponse.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Remove transfer-encoding to avoid conflicts
            context.Response.Headers.Remove("transfer-encoding");

            await proxyResponse.Content.CopyToAsync(context.Response.Body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proxy request to {TargetUrl} failed", targetUrl);
            context.Response.StatusCode = 502;
            await context.Response.WriteAsJsonAsync(new { error = "Upstream service unavailable" });
        }
    }

    private static bool ShouldForwardHeader(string headerName)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Host", "Connection", "Transfer-Encoding",
            "PAYMENT-SIGNATURE", "PAYMENT-REQUIRED", "X-PAYMENT",
            "Authorization"
        };
        return !skip.Contains(headerName);
    }
}
