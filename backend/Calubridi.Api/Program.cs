using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Calubridi.Api.Data;
using Calubridi.Api.Store;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var googleId = builder.Configuration["Google:ClientId"];
var googleSecret = builder.Configuration["Google:ClientSecret"];
var googleEnabled = !string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret);

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connection) && builder.Configuration["Database:Url"] is { Length: > 0 } dbUrl)
{
    var uri = new Uri(dbUrl);
    var credentials = uri.UserInfo.Split(':', 2);
    if (uri.Scheme is not ("postgresql" or "postgres") || credentials.Length != 2)
        throw new InvalidOperationException("Database:Url debe ser una URL PostgreSQL con credenciales.");
    connection = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host, Port = uri.Port < 0 ? 5432 : uri.Port,
        Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        Username = Uri.UnescapeDataString(credentials[0]), Password = Uri.UnescapeDataString(credentials[1]),
        SslMode = SslMode.Require
    }.ConnectionString;
}
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddIdentity<StoreUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.AllowedForNewUsers = true;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

var secureCookies = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Calibridi.Session";
    options.Cookie.SecurePolicy = secureCookies;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});
if (googleEnabled)
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleId!;
        options.ClientSecret = googleSecret!;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClaimActions.MapJsonKey("google_email_verified", "verified_email");
    });
}
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = secureCookies;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("inquiries", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(10) }));
    options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
if (args.Contains("--initialize"))
{
    await StoreSetup.InitializeAsync(app.Services, app.Configuration);
    return;
}

if (app.Configuration.GetValue<bool>("Store:InitializeOnStart"))
    await StoreSetup.InitializeAsync(app.Services, app.Configuration);

app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else app.UseHsts();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; media-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/healthz", () => Results.Ok());
app.MapGet("/api/account/session", (HttpContext context, IAntiforgery antiforgery) =>
    Results.Ok(new
    {
        user = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null,
        admin = context.User.IsInRole("Admin"),
        google = googleEnabled,
        demo = app.Configuration.GetValue<bool>("Store:SeedDemo"),
        csrf = antiforgery.GetAndStoreTokens(context).RequestToken
    }));
app.Run();
