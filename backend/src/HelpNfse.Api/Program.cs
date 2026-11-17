using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HelpNfse.Api.Endpoints;
using HelpNfse.Api.Infra;
using HelpNfse.Core;
using HelpNfse.Core.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--migrate"))
{
    // Usado pelo serviço "migrate" do deploy: aplica as migrações com o usuário migrator e encerra.
    Environment.ExitCode = await Migracoes.ExecutarAsync(builder.Configuration);
    return;
}

builder.Services.AddHelpNfseCore(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorErros>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

// ---- Autenticação: ASP.NET Core Identity (um administrador, sem cadastro público) ----
builder.Services.AddIdentityCore<IdentityUser>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireDigit = false;
        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "helpnfse.sessao";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Chaves de proteção de dados no banco: sessões sobrevivem a reinícios/deploys sem volume de disco.
builder.Services.AddDataProtection().SetApplicationName("help-nfse").PersistKeysToDbContext<AppDbContext>();

// CSRF: o Angular lê o cookie XSRF-TOKEN e devolve o header X-XSRF-TOKEN.
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    o.Cookie.Name = "helpnfse.af";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CabecalhosSeguranca>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ValidacaoAntiforgery>();

app.MapGet("/health", Saude.VerificarAsync).AllowAnonymous().ExcludeFromDescription();

var api = app.MapGroup("/api");
api.MapAuthEndpoints();
api.MapSistemaEndpoints();
api.MapEmissorEndpoints();
api.MapRecorrenciaEndpoints();
api.MapCompetenciaEndpoints();
api.MapNotaEndpoints();
api.MapFallback(() => Results.Problem("Recurso não encontrado.", statusCode: 404)).AllowAnonymous();

// SPA Angular (arquivos estáticos em wwwroot). Os dados ficam todos atrás de /api, autenticados.
app.MapFallbackToFile("index.html").AllowAnonymous();

await Inicializacao.ExecutarAsync(app);
app.Run();

public partial class Program;
