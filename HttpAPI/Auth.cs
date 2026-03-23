using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace HttpAPI;

public static class Auth
{
    /// <summary>
    /// Gets the authenticated user from function context
    /// </summary>
    public static ClaimsPrincipal? GetUser(this FunctionContext context)
    {
        return context.Items.TryGetValue("User", out var user) ? user as ClaimsPrincipal : null;
    }

    /// <summary>
    /// Checks if the request is authenticated
    /// </summary>
    public static bool IsAuthenticated(this FunctionContext context)
    {
        return context.Items.TryGetValue("IsAuthenticated", out var isAuth) && isAuth is true;
    }

    /// <summary>
    /// Gets user ID (oid claim) from function context
    /// </summary>
    public static string? GetUserId(this FunctionContext context)
    {
        var user = context.GetUser();
        return user?.FindFirst("oid")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Gets user email from function context
    /// </summary>
    public static string? GetUserEmail(this FunctionContext context)
    {
        var user = context.GetUser();
        return user?.FindFirst("preferred_username")?.Value
            ?? user?.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Gets user name from function context
    /// </summary>
    public static string? GetUserName(this FunctionContext context)
    {
        return context.GetUser()?.Identity?.Name;
    }
}
