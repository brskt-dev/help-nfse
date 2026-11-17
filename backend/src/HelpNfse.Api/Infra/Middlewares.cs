using HelpNfse.Core.Dados;
using HelpNfse.Core.Servicos;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Api.Infra;

/// <summary>Converte erros de negócio em ProblemDetails com mensagem em português; oculta detalhes de erros internos.</summary>
public sealed class TratadorErros(IProblemDetailsService problemDetails, ILogger<TratadorErros> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, titulo, erros) = ex switch
        {
            ErroNegocio e => (e.Status, e.Message, e.Detalhes),
            BadHttpRequestException => (400, "Requisição inválida. Verifique os dados enviados.", (IReadOnlyList<string>)[]),
            _ => (500, "Erro inesperado. Tente novamente; se persistir, verifique os logs do servidor.", []),
        };
        if (status >= 500) log.LogError(ex, "Erro não tratado em {Caminho}", ctx.Request.Path);

        ctx.Response.StatusCode = status;
        var pd = new ProblemDetails { Status = status, Title = titulo };
        if (erros.Count > 0) pd.Extensions["erros"] = erros;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = pd, Exception = ex });
    }
}

/// <summary>Valida o token antiforgery em toda requisição de escrita na API.</summary>
public sealed class ValidacaoAntiforgery(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IAntiforgery antiforgery)
    {
        var m = ctx.Request.Method;
        if (ctx.Request.Path.StartsWithSegments("/api")
            && !(HttpMethods.IsGet(m) || HttpMethods.IsHead(m) || HttpMethods.IsOptions(m)))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
            }
            catch (AntiforgeryValidationException)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 400, Title = "Sessão expirada ou requisição inválida. Recarregue a página e tente novamente.",
                    Type = "antiforgery",
                });
                return;
            }
        }
        await next(ctx);
    }

    /// <summary>Emite o cookie legível pelo Angular com o token da sessão atual.</summary>
    public static void EmitirToken(HttpContext ctx, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(ctx);
        ctx.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, SameSite = SameSiteMode.Strict, Secure = ctx.Request.IsHttps, Path = "/",
        });
    }
}

public sealed class CabecalhosSeguranca(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
            "font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        if (ctx.Request.Path.StartsWithSegments("/api")) h.CacheControl = "no-store";
        return next(ctx);
    }
}

public static class Saude
{
    public static async Task<IResult> VerificarAsync(IDbContextFactory<AppDbContext> dbFactory, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await using var db = await dbFactory.CreateDbContextAsync(cts.Token);
            return await db.Database.CanConnectAsync(cts.Token)
                ? Results.Text("ok")
                : Results.Text("banco indisponível", statusCode: 503);
        }
        catch
        {
            return Results.Text("banco indisponível", statusCode: 503);
        }
    }
}
