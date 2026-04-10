using X402.Gateway.Config;
using X402.Gateway.Discovery;

namespace X402.Gateway.Tests;

public class DiscoveryTests
{
    [Fact]
    public void BuildDiscovery_GeneratesCorrectItems()
    {
        var config = new X402GatewayConfig
        {
            WalletAddress = "GABCDEF",
            FacilitatorUrl = "https://facilitator.example.com",
            Network = "stellar:testnet",
            Routes = new[]
            {
                new PaidRoute
                {
                    Method = "GET",
                    Path = "/weather",
                    Price = 0.01m,
                    Description = "Weather data"
                },
                new PaidRoute
                {
                    Method = "POST",
                    Path = "/translate",
                    Price = 0.05m
                }
            }
        };

        var discovery = WellKnownX402Handler.BuildDiscovery(config, "https://pay.paywit.io");

        Assert.Equal(2, discovery.Items.Length);
        Assert.Equal("https://pay.paywit.io/weather", discovery.Items[0].Resource);
        Assert.Equal("https://pay.paywit.io/translate", discovery.Items[1].Resource);
        Assert.Equal(2, discovery.X402Version);
        Assert.Single(discovery.Items[0].Accepts);
        Assert.Equal("exact", discovery.Items[0].Accepts[0].Scheme);
        Assert.Equal("stellar:testnet", discovery.Items[0].Accepts[0].Network);
        Assert.Equal("GABCDEF", discovery.Items[0].Accepts[0].PayTo);
    }
}
