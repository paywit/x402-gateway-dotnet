using X402.Gateway.Config;

namespace X402.Gateway.Tests;

public class ConfigTests
{
    [Fact]
    public void YamlConfigLoader_ParsesValidConfig()
    {
        var yaml = @"
merchant:
  apiBaseUrl: https://api.acme.com
  authHeader: X-API-Key
  authValue: sk_live_abc123
routes:
  - path: GET /weather
    price: 0.01
    asset: USDC
    description: Weather data
  - path: POST /translate
    price: 0.05
    asset: USDC
wallet:
  address: GABCDEFGHIJKLMNOPQRSTUVWXYZ234567ABCDEFGHIJKLMNOPQRSTUV
  network: stellar:testnet
facilitator:
  url: https://channels.openzeppelin.com/x402/testnet
  apiKey: test-key-123
webhook:
  url: https://webhook.acme.com/payments
";

        var config = YamlConfigLoader.LoadFromString(yaml);

        Assert.Equal("https://api.acme.com", config.ProxyBaseUrl);
        Assert.Equal("X-API-Key", config.ProxyAuthHeader);
        Assert.Equal("sk_live_abc123", config.ProxyAuthValue);
        Assert.Equal("GABCDEFGHIJKLMNOPQRSTUVWXYZ234567ABCDEFGHIJKLMNOPQRSTUV", config.WalletAddress);
        Assert.Equal("stellar:testnet", config.Network);
        Assert.Equal("https://channels.openzeppelin.com/x402/testnet", config.FacilitatorUrl);
        Assert.Equal("test-key-123", config.FacilitatorApiKey);
        Assert.Equal("https://webhook.acme.com/payments", config.WebhookUrl);
        Assert.Equal(2, config.Routes.Length);

        Assert.Equal("GET", config.Routes[0].Method);
        Assert.Equal("/weather", config.Routes[0].Path);
        Assert.Equal(0.01m, config.Routes[0].Price);
        Assert.Equal("Weather data", config.Routes[0].Description);

        Assert.Equal("POST", config.Routes[1].Method);
        Assert.Equal("/translate", config.Routes[1].Path);
        Assert.Equal(0.05m, config.Routes[1].Price);
    }

    [Fact]
    public void PaidRoute_RouteKey_FormatsCorrectly()
    {
        var route = new PaidRoute { Method = "get", Path = "/weather", Price = 0.01m };
        Assert.Equal("GET /weather", route.RouteKey);
    }
}
