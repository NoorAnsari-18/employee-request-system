using System.Net;

namespace EmployeeRequests.Api.HubSpot;

/// <summary>Retries HubSpot calls up to twice (0.5s, 1s backoff) on rate limiting (429) or server errors (5xx).</summary>
public sealed class RetryHandler(ILogger<RetryHandler> logger) : DelegatingHandler
{
    private const int MaxRetries = 2;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await base.SendAsync(request, ct);
            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (!retryable || attempt == MaxRetries)
                return response;

            logger.LogWarning("HubSpot {Method} {Path} returned {Status}; retry {Attempt}/{Max}",
                request.Method, request.RequestUri?.AbsolutePath, (int)response.StatusCode, attempt + 1, MaxRetries);
            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(500 * (1 << attempt)), ct);
        }
    }
}
