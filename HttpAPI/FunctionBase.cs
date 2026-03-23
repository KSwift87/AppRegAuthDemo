using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace HttpAPI;

public abstract class FunctionBase
{
    public async Task<IActionResult> ExecuteAsync(
        Func<Task<IActionResult>> httpRequestFunc,
        FunctionContext ctx,
        string? role = null
    )
    {
        var authResponse = await RequireAuthenticationAsync(ctx);
        if (authResponse != null)
            return authResponse;

        if (!string.IsNullOrEmpty(role))
        {
            authResponse = await RequireRoleAsync(ctx, role);
            if (authResponse != null)
                return authResponse;
        }

        return await httpRequestFunc.Invoke();
    }

    public async Task<IActionResult> ExecuteAsync(
        Func<IActionResult> httpRequestFunc,
        FunctionContext ctx,
        IEnumerable<string>? roles = null
    )
    {
        var authResponse = await RequireAuthenticationAsync(ctx);
        if (authResponse != null)
            return authResponse;

        if (roles != null && roles.Any())
        {
            authResponse = await RequireAnyRoleAsync(ctx, roles.ToArray());
            if (authResponse != null)
                return authResponse;
        }

        return httpRequestFunc.Invoke();
    }

    /// <summary>
    /// Gets all user roles from function context
    /// </summary>
    private IEnumerable<string> GetRoles(FunctionContext context)
    {
        var user = context.GetUser();
        return user?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// Requires authentication, returns 401 IActionResult if not authenticated
    /// </summary>
    private Task<IActionResult?> RequireAuthenticationAsync(FunctionContext context)
    {
        if (!context.IsAuthenticated())
        {
            return Task.FromResult<IActionResult?>(
                new UnauthorizedObjectResult(new { Error = "Authentication required" }));
        }

        return Task.FromResult<IActionResult?>(null);
    }

    /// <summary>
    /// Requires specific role, returns 403 IActionResult if user doesn't have role
    /// </summary>
    private async Task<IActionResult?> RequireRoleAsync(FunctionContext context, string role)
    {
        var authResponse = await RequireAuthenticationAsync(context);
        if (authResponse != null) return authResponse;

        var user = context.GetUser();
        if (user == null || !user.IsInRole(role))
        {
            return new ObjectResult(new { Error = $"Forbidden: {role} role required" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return null;
    }

    /// <summary>
    /// Requires any of the specified roles
    /// </summary>
    private async Task<IActionResult?> RequireAnyRoleAsync(FunctionContext context, params string[] roles)
    {
        var authResponse = await RequireAuthenticationAsync(context);
        if (authResponse != null) return authResponse;

        var user = context.GetUser();
        var hasAnyRole = roles.Any(role => user?.IsInRole(role) == true);

        if (!hasAnyRole)
        {
            return new ObjectResult(new
            {
                Error = $"Forbidden: One of the following roles required: {string.Join(", ", roles)}"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return null;
    }

    /// <summary>
    /// Checks authentication and returns IActionResult or null if authorized (non-async version)
    /// </summary>
    private IActionResult? CheckAuthentication(FunctionContext context)
    {
        if (!context.IsAuthenticated())
        {
            return new UnauthorizedObjectResult(new { error = "Authentication required" });
        }

        return null;
    }

    /// <summary>
    /// Checks role and returns IActionResult or null if authorized
    /// </summary>
    private IActionResult? CheckRole(FunctionContext context, string role)
    {
        var authResult = CheckAuthentication(context);
        if (authResult != null) return authResult;

        var user = context.GetUser();
        if (user == null || !user.IsInRole(role))
        {
            return new ObjectResult(new { Error = $"Forbidden: {role} role required" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return null;
    }

    /// <summary>
    /// Checks if user has any of the specified roles
    /// </summary>
    private IActionResult? CheckAnyRole(FunctionContext context, params string[] roles)
    {
        var authResult = CheckAuthentication(context);
        if (authResult != null) return authResult;

        var user = context.GetUser();
        var hasAnyRole = roles.Any(role => user?.IsInRole(role) == true);

        if (!hasAnyRole)
        {
            return new ObjectResult(new
            {
                Error = $"Forbidden: One of the following roles required: {string.Join(", ", roles)}"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return null;
    }
}
