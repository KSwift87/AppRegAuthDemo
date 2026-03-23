using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace HttpAPI;

public class AuthMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<AuthMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private TokenValidationParameters? _validationParameters;

    public AuthMiddleware(
        ILogger<AuthMiddleware> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();

        if (requestData != null)
        {
            // Extract authorization header
            if (requestData.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var authHeader = authHeaders.FirstOrDefault();

                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();

                    try
                    {
                        var claimsPrincipal = await ValidateTokenAsync(token);

                        if (claimsPrincipal != null)
                        {
                            // ✅ Store the validated principal in FunctionContext
                            context.Items["User"] = claimsPrincipal;
                            context.Items["IsAuthenticated"] = true;

                            // ✅ CRITICAL: Also populate HttpContext.User for extension methods
                            var httpContext = requestData.FunctionContext.GetHttpContext();
                            if (httpContext != null)
                            {
                                httpContext.User = claimsPrincipal;
                                _logger.LogDebug("HttpContext.User populated with validated principal");
                            }

                            var userId = claimsPrincipal.FindFirst("oid")?.Value
                                ?? claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                            _logger.LogInformation("Token validated for user: {UserId}", userId);
                        }
                        else
                        {
                            _logger.LogWarning("Token validation returned null principal");
                            context.Items["IsAuthenticated"] = false;
                        }
                    }
                    catch (SecurityTokenException ex)
                    {
                        _logger.LogWarning(ex, "Token validation failed: {Message}", ex.Message);
                        context.Items["IsAuthenticated"] = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error validating token");
                        context.Items["IsAuthenticated"] = false;
                    }
                }
                else
                {
                    _logger.LogDebug("No Bearer token found in Authorization header");
                    context.Items["IsAuthenticated"] = false;
                }
            }
            else
            {
                _logger.LogDebug("No Authorization header present");
                context.Items["IsAuthenticated"] = false;
            }
        }

        await next(context);
    }

    private async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        if (_validationParameters == null)
        {
            _validationParameters = await GetTokenValidationParametersAsync();
        }

        try
        {
            var principal = _tokenHandler.ValidateToken(token, _validationParameters, out var validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }

    private async Task<TokenValidationParameters> GetTokenValidationParametersAsync()
    {
        var azureAdConfig = _configuration.GetSection("AzureAd");
        var tenantId = azureAdConfig["TenantId"];
        var clientId = azureAdConfig["ClientId"];
        var instance = azureAdConfig["Instance"] ?? "https://login.microsoftonline.com/";

        var authority = $"{instance.TrimEnd('/')}/{tenantId}/v2.0";
        var metadataAddress = $"{authority}/.well-known/openid-configuration";

        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

        var config = await configManager.GetConfigurationAsync();

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                $"https://sts.windows.net/{tenantId}/"
            },

            ValidateAudience = true,
            ValidAudiences = new[]
            {
                clientId,
                $"api://{clientId}"
            },

            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),

            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
    }
}
