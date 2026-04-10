using Microsoft.AspNetCore.Http;
using X402.Gateway.Config;
using X402.Stellar.Http;
using X402.Stellar.Types;

namespace X402.Gateway.Discovery;

public static class WellKnownX402Handler
{
    public static DiscoveryResponse BuildDiscovery(X402GatewayConfig config, string baseUrl)
    {
        var items = config.Routes.Select(route =>
        {
            var amount = X402ResponseBuilder.AmountFromDecimal(route.Price);
            var requirements = X402ResponseBuilder.BuildStellarRequirements(
                payTo: config.WalletAddress,
                amount: amount,
                network: config.Network
            );

            return new DiscoveryResource
            {
                Resource = $"{baseUrl.TrimEnd('/')}{route.Path}",
                Accepts = new[] { requirements },
                Metadata = route.Description is not null
                    ? new Dictionary<string, string> { ["description"] = route.Description }
                    : null
            };
        }).ToArray();

        return new DiscoveryResponse { Items = items };
    }

    public static async Task HandleRequest(HttpContext context, X402GatewayConfig config)
    {
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        var discovery = BuildDiscovery(config, baseUrl);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(discovery);
    }
}
