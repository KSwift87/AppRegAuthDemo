using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using SsrClientApp.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);

        // Authorization code grant flow.
        options.ResponseType = "code";
        options.UsePkce = true;

        options.SaveTokens = true;

        // Look at the audience claim before and after uncommenting the following.
        //options.Scope.Add("api://client-id/Api.Execute");
    });

builder.Services.AddHttpContextAccessor();

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Mandatory for auth.
app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

// Authentication endpoints
app.MapGet("/authentication/login", async (HttpContext context, string? returnUrl) =>
{
    var redirectUrl = returnUrl ?? "/";
    return Results.Challenge(new AuthenticationProperties { RedirectUri = redirectUrl },
        new[] { OpenIdConnectDefaults.AuthenticationScheme });
});

app.MapGet("/authentication/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);

    return Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, new[] { OpenIdConnectDefaults.AuthenticationScheme });
});

// Debug endpoint to view tokens (remove in production)
app.MapGet("/debug/tokens", async (HttpContext context) =>
{
    if (!context.User.Identity?.IsAuthenticated ?? true)
    {
        return Results.Unauthorized();
    }

    var accessToken = await context.GetTokenAsync("access_token");
    var idToken = await context.GetTokenAsync("id_token");
    var refreshToken = await context.GetTokenAsync("refresh_token");

    return Results.Ok(new
    {
        accessToken = accessToken,
        idToken = idToken,
        refreshToken = refreshToken,
        claims = context.User.Claims.Select(c => new { c.Type, c.Value })
    });
});

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
