using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace SsrClientApp;

/// <summary>
/// Delegating handler that automatically attaches bearer token to HTTP requests
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BearerTokenHandler> _logger;

    public BearerTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<BearerTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            // ✅ Get access token from server-side authentication cookie
            var accessToken = await httpContext.GetTokenAsync("access_token");
            var idToken = await httpContext.GetTokenAsync("id_token"); // For debugging purposes only.

            if (!string.IsNullOrEmpty(accessToken))
            {
                // ✅ Attach bearer token to request
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                _logger.LogDebug($"Attached bearer token to request: { request.RequestUri}");
            }
            else
            {
                _logger.LogWarning($"Access token not available for request: {request.RequestUri}");
            }
        }
        else
        {
            _logger.LogWarning("HttpContext is null, cannot attach token");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
