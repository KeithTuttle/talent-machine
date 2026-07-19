using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TalentMachine.Api.Auth;
using TalentMachine.Api.Data;
using TalentMachine.Api.Services;

// QuestPDF Community license (required, set once at startup).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Load user-secrets explicitly by id. NOT gated on the environment: VS Code's
// `dotnet run` can default to the Production environment (when it doesn't apply
// launchSettings), which would otherwise skip user-secrets and silently fall
// back to the localhost placeholder. On a real deployment the secrets file
// doesn't exist, so this is a harmless no-op there and the platform's
// ConnectionStrings__Default env var is used instead.
builder.Configuration.AddUserSecrets("f3f99397-b9fd-41de-8e0f-2b864a92eae6");

// Belt-and-braces: user-secrets live under %APPDATA%, and at least one launcher
// (VS Code's task runner) spawns `dotnet run` with an environment where that
// path doesn't resolve, so the secret silently fails to load. This gitignored
// file sits next to the app, so it loads no matter who spawned the process.
// It intentionally comes AFTER AddUserSecrets so it wins when both exist.
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: false);

const string CorsPolicy = "spa";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Per-request tenant, resolved by TenantResolutionMiddleware, read by AppDbContext.
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();

// Printable rehearsal schedules (QuestPDF).
builder.Services.AddScoped<RehearsalPdfService>();
// AI rehearsal-schedule suggestions via Google Gemini (free tier).
builder.Services.AddHttpClient();
builder.Services.AddScoped<RehearsalAiService>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize enums as strings in API payloads to match the DB storage.
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Authentication (Clerk, via JWT bearer) ---
// Enabled only when Clerk:Authority is configured. Until then the API boots
// unauthenticated (dev convenience) — a startup warning is logged.
var clerkAuthority = builder.Configuration["Clerk:Authority"];
var authEnabled = !string.IsNullOrWhiteSpace(clerkAuthority);

if (authEnabled)
{
    var authorizedParty = builder.Configuration["Clerk:AuthorizedParty"];

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = clerkAuthority;
            // Keep the raw Clerk claim names (notably `sub`) instead of remapping.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = clerkAuthority,
                // Clerk session tokens have no fixed audience; we validate the
                // authorized party (azp) below instead.
                ValidateAudience = false,
                ValidateLifetime = true,
                NameClaimType = "sub",
            };

            if (!string.IsNullOrWhiteSpace(authorizedParty))
            {
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ctx =>
                    {
                        var azp = ctx.Principal?.FindFirst("azp")?.Value;
                        if (!string.IsNullOrEmpty(azp) && azp != authorizedParty)
                            ctx.Fail("Unauthorized party (azp mismatch).");
                        return Task.CompletedTask;
                    },
                };
            }
        });

    // Every endpoint requires an authenticated user unless it opts out
    // with [AllowAnonymous] (e.g. the health check).
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}
else
{
    builder.Services.AddAuthorization();
}

// Allowed SPA origins: config key "Cors:AllowedOrigins" (env var Cors__AllowedOrigins),
// a comma-separated list, e.g. "https://talentmachine.vercel.app". Always includes
// the local Vite dev origin so local dev keeps working unmodified.
var configuredOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var allowedOrigins = configuredOrigins.Append("http://localhost:5201").Distinct().ToArray();

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Render (and similar PaaS hosts) assign the listen port via the PORT env var
// at runtime rather than a fixed one; Kestrel must bind to it explicitly.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Log which database we actually resolved (no secrets) so a config that silently
// fell back to the localhost placeholder is obvious in the terminal instead of a
// wall of "connection refused" errors. Locally the docker-compose Postgres IS on
// localhost, so this is informational there.
var dbHost = "unknown";
try
{
    dbHost = new Npgsql.NpgsqlConnectionStringBuilder(
        builder.Configuration.GetConnectionString("Default")).Host ?? "unknown";
}
catch { /* unparseable / missing */ }

app.Logger.LogInformation(
    "Environment: {Env}; Database host: {DbHost}{Note}",
    app.Environment.EnvironmentName, dbHost,
    dbHost is "localhost" or "127.0.0.1"
        ? " (appsettings placeholder — fine with the docker-compose DB; if you expected Supabase, your appsettings.Development.local.json/user-secrets did not load, or you're running a stale build)"
        : string.Empty);

app.Logger.LogInformation("CORS allowed origins: {Origins}", string.Join(", ", allowedOrigins));

if (!authEnabled)
{
    app.Logger.LogWarning(
        "Clerk:Authority is not configured — the API is running UNAUTHENTICATED and tenant " +
        "isolation is INACTIVE. Set Clerk:Authority (and the client's VITE_CLERK_PUBLISHABLE_KEY) to enable auth.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

if (authEnabled)
    app.UseAuthentication();

app.UseAuthorization();

// Resolve the caller's tenant (and auto-provision on first login) after auth.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.Run();
