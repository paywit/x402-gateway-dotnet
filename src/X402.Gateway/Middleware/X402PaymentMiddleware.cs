using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using X402.Gateway.Config;
using X402.Stellar.Client;
using X402.Stellar.Constants;
using X402.Stellar.Http;
using X402.Stellar.Types;

namespace X402.Gateway.Middleware;

public sealed class X402PaymentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly X402GatewayConfig _config;
    private readonly IFacilitatorClient _facilitatorClient;
    private readonly ILogger<X402PaymentMiddleware> _logger;
    private readonly Dictionary<string, PaidRoute> _routeMap;

    public X402PaymentMiddleware(
        RequestDelegate next,
        X402GatewayConfig config,
        IFacilitatorClient facilitatorClient,
        ILogger<X402PaymentMiddleware> logger)
    {
        _next = next;
        _config = config;
        _facilitatorClient = facilitatorClient;
        _logger = logger;
        _routeMap = config.Routes.ToDictionary(r => r.RouteKey, r => r, StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var routeKey = $"{context.Request.Method} {context.Request.Path}";
        if (!TryMatchRoute(routeKey, out var route))
        {
            await _next(context);
            return;
        }

        var headers = context.Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.FirstOrDefault() ?? "", StringComparer.OrdinalIgnoreCase);

        var paymentResult = X402PaymentHeaderParser.Parse(headers);

        if (!paymentResult.HasPayment)
        {
            await Return402(context, route!);
            return;
        }

        var payload = paymentResult.PaymentPayload!;
        var requirements = BuildRequirements(route!);

        // Step 1: Verify payment with facilitator
        VerifyResponse verifyResponse;
        try
        {
            verifyResponse = await _facilitatorClient.VerifyAsync(payload, requirements);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facilitator verify failed for {RouteKey}", routeKey);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Payment verification failed" });
            return;
        }

        if (!verifyResponse.IsValid)
        {
            _logger.LogWarning("Payment invalid: {Reason} - {Message}", verifyResponse.InvalidReason, verifyResponse.InvalidMessage);
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid payment",
                reason = verifyResponse.InvalidReason,
                message = verifyResponse.InvalidMessage
            });
            return;
        }

        _logger.LogInformation("Payment verified for {RouteKey} from {Payer}", routeKey, verifyResponse.Payer);

        // Step 2: Buffer the response from the downstream handler/proxy
        var originalBodyStream = context.Response.Body;
        using var bufferedBody = new MemoryStream();
        context.Response.Body = bufferedBody;

        await _next(context);

        // Step 3: If downstream succeeded, settle the payment
        if (context.Response.StatusCode < 400)
        {
            try
            {
                var settleResponse = await _facilitatorClient.SettleAsync(payload, requirements);

                if (settleResponse.Success)
                {
                    _logger.LogInformation("Payment settled: tx={Transaction} payer={Payer} amount={Amount}",
                        settleResponse.Transaction, settleResponse.Payer, settleResponse.Amount);

                    var encodedResponse = X402HeaderCodec.EncodeSettleResponse(settleResponse);
                    context.Response.Headers[X402Constants.PaymentResponseHeader] = encodedResponse;
                }
                else
                {
                    _logger.LogError("Settlement failed: {Reason} - {Message}",
                        settleResponse.ErrorReason, settleResponse.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Settlement exception for {RouteKey}", routeKey);
            }
        }
        else
        {
            _logger.LogInformation("Downstream returned {StatusCode}, skipping settlement", context.Response.StatusCode);
        }

        // Replay buffered response
        bufferedBody.Seek(0, SeekOrigin.Begin);
        context.Response.Body = originalBodyStream;
        await bufferedBody.CopyToAsync(originalBodyStream);
    }

    private bool TryMatchRoute(string routeKey, out PaidRoute? route)
    {
        if (_routeMap.TryGetValue(routeKey, out route))
            return true;

        // Try wildcard path matching
        foreach (var kvp in _routeMap)
        {
            var parts = kvp.Key.Split(' ', 2);
            if (parts.Length != 2) continue;

            var method = parts[0];
            var pattern = parts[1];

            var reqParts = routeKey.Split(' ', 2);
            if (reqParts.Length != 2) continue;

            if (!string.Equals(method, reqParts[0], StringComparison.OrdinalIgnoreCase))
                continue;

            if (PathMatchesPattern(reqParts[1], pattern))
            {
                route = kvp.Value;
                return true;
            }
        }

        route = null;
        return false;
    }

    private static bool PathMatchesPattern(string path, string pattern)
    {
        if (pattern.EndsWith("/*"))
        {
            var prefix = pattern[..^2];
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private PaymentRequirements BuildRequirements(PaidRoute route)
    {
        var amount = X402ResponseBuilder.AmountFromDecimal(route.Price);
        return X402ResponseBuilder.BuildStellarRequirements(
            payTo: _config.WalletAddress,
            amount: amount,
            network: _config.Network
        );
    }

    private async Task Return402(HttpContext context, PaidRoute route)
    {
        var requirements = BuildRequirements(route);
        var resourceUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";

        var paymentRequired = X402ResponseBuilder.BuildPaymentRequired(
            resourceUrl: resourceUrl,
            accepts: new[] { requirements },
            description: route.Description,
            mimeType: route.MimeType
        );

        var encoded = X402HeaderCodec.EncodePaymentRequired(paymentRequired);
        context.Response.StatusCode = 402;
        context.Response.Headers[X402Constants.PaymentRequiredHeader] = encoded;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(paymentRequired);
    }
}
