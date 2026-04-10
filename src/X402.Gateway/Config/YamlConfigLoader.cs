using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace X402.Gateway.Config;

public static class YamlConfigLoader
{
    public static X402GatewayConfig LoadFromFile(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return LoadFromString(yaml);
    }

    public static X402GatewayConfig LoadFromString(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var raw = deserializer.Deserialize<YamlConfig>(yaml);
        return MapToConfig(raw);
    }

    private static X402GatewayConfig MapToConfig(YamlConfig raw)
    {
        var routes = raw.Routes?.Select(r =>
        {
            var parts = r.Path.Split(' ', 2);
            return new PaidRoute
            {
                Method = parts.Length > 1 ? parts[0] : "GET",
                Path = parts.Length > 1 ? parts[1] : parts[0],
                Price = r.Price,
                Asset = r.Asset ?? "USDC",
                Description = r.Description
            };
        }).ToArray() ?? Array.Empty<PaidRoute>();

        return new X402GatewayConfig
        {
            WalletAddress = raw.Wallet?.Address ?? throw new InvalidOperationException("wallet.address is required"),
            FacilitatorUrl = raw.Facilitator?.Url ?? throw new InvalidOperationException("facilitator.url is required"),
            FacilitatorApiKey = raw.Facilitator?.ApiKey,
            Network = raw.Wallet?.Network ?? "stellar:testnet",
            Routes = routes,
            ProxyBaseUrl = raw.Merchant?.ApiBaseUrl,
            ProxyAuthHeader = raw.Merchant?.AuthHeader,
            ProxyAuthValue = raw.Merchant?.AuthValue,
            WebhookUrl = raw.Webhook?.Url
        };
    }

    private sealed class YamlConfig
    {
        public MerchantConfig? Merchant { get; set; }
        public WalletConfig? Wallet { get; set; }
        public FacilitatorConfig? Facilitator { get; set; }
        public RouteConfig[]? Routes { get; set; }
        public WebhookConfig? Webhook { get; set; }
    }

    private sealed class MerchantConfig
    {
        public string? ApiBaseUrl { get; set; }
        public string? AuthHeader { get; set; }
        public string? AuthValue { get; set; }
    }

    private sealed class WalletConfig
    {
        public string? Address { get; set; }
        public string? Network { get; set; }
    }

    private sealed class FacilitatorConfig
    {
        public string? Url { get; set; }
        public string? ApiKey { get; set; }
    }

    private sealed class RouteConfig
    {
        public string Path { get; set; } = "";
        public decimal Price { get; set; }
        public string? Asset { get; set; }
        public string? Description { get; set; }
    }

    private sealed class WebhookConfig
    {
        public string? Url { get; set; }
    }
}
