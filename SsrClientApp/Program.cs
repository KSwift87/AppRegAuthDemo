using Azure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using SsrClientApp;
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
        options.Scope.Add("api://0a7a89df-ff2b-47d4-85ad-e07b3f2ccb95/Api.Execute");
    });

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<BearerTokenHandler>();

// Configure IHttpClientFactory for API
builder.Services.AddHttpClient("HttpAPI", httpClient =>
{
    httpClient.BaseAddress = new Uri("http://localhost:7161");
    httpClient.Timeout = TimeSpan.FromSeconds(360);
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<BearerTokenHandler>();

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

// HttpAPI endpoints
app.MapGet("/Function1", async (HttpContext context) =>
{
    using var httpClient = context.RequestServices
        .GetRequiredService<IHttpClientFactory>()
        .CreateClient("HttpAPI");

    var response = await httpClient.GetAsync("/api/Function1");

    // Ensure the request was successful
    response.EnsureSuccessStatusCode();

    // Read and return the content
    var content = await response.Content.ReadAsStringAsync();
    return Results.Ok(content);
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
