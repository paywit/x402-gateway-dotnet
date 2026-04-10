namespace X402.Gateway.Config;

public sealed class X402GatewayConfig
{
    public required string WalletAddress { get; set; }
    public required string FacilitatorUrl { get; set; }
    public string? FacilitatorApiKey { get; set; }
    public string Network { get; set; } = "stellar:testnet";
    public required PaidRoute[] Routes { get; set; }

    public string? ProxyBaseUrl { get; set; }
    public string? ProxyAuthHeader { get; set; }
    public string? ProxyAuthValue { get; set; }

    public string? WebhookUrl { get; set; }
}
