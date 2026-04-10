using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using X402.Gateway.Config;
using X402.Gateway.Discovery;
using X402.Gateway.Middleware;
using X402.Gateway.Proxy;
using X402.Stellar.Client;

namespace X402.Gateway;

public static class X402GatewayExtensions
{
    public static IServiceCollection AddX402Gateway(this IServiceCollection services, X402GatewayConfig config)
    {
        services.AddSingleton(config);

        services.AddSingleton<IFacilitatorClient>(_ => new FacilitatorClient(
            new FacilitatorClientOptions
            {
                Url = config.FacilitatorUrl,
                ApiKey = config.FacilitatorApiKey
            }));

        services.AddHttpClient<ReverseProxyMiddleware>();

        return services;
    }

    public static IApplicationBuilder UseX402Gateway(this IApplicationBuilder app)
    {
        return UseX402Gateway(app, app.ApplicationServices.GetRequiredService<X402GatewayConfig>());
    }

    public static IApplicationBuilder UseX402Gateway(this IApplicationBuilder app, X402GatewayConfig config)
    {
        // Register .well-known/x402 endpoint
        app.Map("/.well-known/x402", appBuilder =>
        {
            appBuilder.Run(context => WellKnownX402Handler.HandleRequest(context, config));
        });

        // Payment middleware intercepts configured routes
        app.UseMiddleware<X402PaymentMiddleware>();

        // If proxy mode is configured, add reverse proxy as terminal middleware
        if (!string.IsNullOrEmpty(config.ProxyBaseUrl))
        {
            app.UseMiddleware<ReverseProxyMiddleware>();
        }

        return app;
    }
}
