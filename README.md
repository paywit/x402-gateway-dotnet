# x402-gateway-dotnet

x402 payment gateway middleware for ASP.NET. Protect API routes with USDC
micropayments on Stellar.

Built on top of
[x402-stellar-dotnet](https://github.com/paywit/x402-stellar-dotnet) — the
protocol library — `x402-gateway-dotnet` is the framework integration that lets
you drop x402 into any ASP.NET API in one line.

## What it does

- **ASP.NET Middleware**: Intercepts requests to configured routes, returns 402
  Payment Required
- **Verify-Proxy-Settle**: Verifies payment with facilitator, proxies to
  merchant API, settles only after merchant returns success
- **YAML Config**: Configure routes, pricing, and merchant API credentials via
  YAML
- **Discovery**: Auto-generates `.well-known/x402` endpoint from route config
- **Reverse Proxy**: Forwards authenticated requests to a merchant's backend API

## Installation

```bash
dotnet add package x402-gateway-dotnet
```

## Usage — Embedded in your ASP.NET API

```csharp
using X402.Gateway;
using X402.Gateway.Config;

builder.Services.AddX402Gateway(new X402GatewayConfig
{
    WalletAddress = "GABCD...",
    FacilitatorUrl = "https://x402.org/facilitator",
    Network = "stellar:testnet",
    Routes = new[]
    {
        new PaidRoute { Method = "GET", Path = "/weather", Price = 0.01m, Asset = "USDC" },
        new PaidRoute { Method = "POST", Path = "/translate", Price = 0.05m, Asset = "USDC" }
    }
});

app.UseX402Gateway();
```

## Usage — Standalone Proxy (YAML config)

```yaml
merchant:
  apiBaseUrl: https://api.acme.com
  authHeader: X-API-Key
  authValue: sk_live_abc123
routes:
  - path: GET /weather
    price: 0.01
    asset: USDC
  - path: POST /translate
    price: 0.05
    asset: USDC
wallet:
  address: GABCD...
facilitator:
  url: https://x402.org/facilitator
```

## How it works

```
Agent → GET /weather → Gateway (no payment?) → 402 + requirements
Agent → signs Stellar tx → GET /weather + PAYMENT-SIGNATURE header
Gateway → /verify → facilitator ✓ → proxy to merchant → 200 → /settle → return response
```

The verify-then-settle ordering protects both sides:

- **Merchant** is protected because payment is verified before hitting their API
- **Agent** is protected because payment is only settled after merchant returns
  success

## License

MIT
