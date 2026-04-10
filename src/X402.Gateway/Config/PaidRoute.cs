namespace X402.Gateway.Config;

public sealed class PaidRoute
{
    public required string Method { get; set; }
    public required string Path { get; set; }
    public required decimal Price { get; set; }
    public string Asset { get; set; } = "USDC";
    public string? Description { get; set; }
    public string? MimeType { get; set; }

    public string RouteKey => $"{Method.ToUpperInvariant()} {Path}";
}
