using System.Security.Claims;
using HelpNfse.Api.Infra;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace HelpNfse.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record LoginDto(string Email, string Senha);
    public sealed record SessaoDto(bool Autenticado, string? Email);
    public sealed record AlterarSenhaDto(string SenhaAtual, string NovaSenha);

    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/auth");

        // Informa a sessão atual e renova o token antiforgery (cookie XSRF-TOKEN).
        g.MapGet("/sessao", (HttpContext ctx, IAntiforgery af) =>
        {
            ValidacaoAntiforgery.EmitirToken(ctx, af);
            var autenticado = ctx.User.Identity?.IsAuthenticated == true;
            return new SessaoDto(autenticado, autenticado ? ctx.User.FindFirstValue(ClaimTypes.Email) ?? ctx.User.Identity!.Name : null);
        }).AllowAnonymous();

        g.MapPost("/login", async (LoginDto dto, SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users) =>
        {
            const string falha = "E-mail ou senha inválidos.";
            var user = string.IsNullOrWhiteSpace(dto.Email) ? null : await users.FindByEmailAsync(dto.Email.Trim());
            if (user is null) return Results.Problem(falha, statusCode: 401);
            var r = await signIn.PasswordSignInAsync(user, dto.Senha ?? "", isPersistent: false, lockoutOnFailure: true);
            if (r.IsLockedOut) return Results.Problem("Acesso bloqueado temporariamente por excesso de tentativas. Aguarde 15 minutos.", statusCode: 423);
            return r.Succeeded ? Results.NoContent() : Results.Problem(falha, statusCode: 401);
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/logout", async (SignInManager<IdentityUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        });

        g.MapPost("/senha", async (AlterarSenhaDto dto, ClaimsPrincipal principal, UserManager<IdentityUser> users,
            SignInManager<IdentityUser> signIn) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var r = await users.ChangePasswordAsync(user, dto.SenhaAtual ?? "", dto.NovaSenha ?? "");
            if (!r.Succeeded)
                return Results.Problem("Não foi possível alterar a senha.", statusCode: 400,
                    extensions: new Dictionary<string, object?> { ["erros"] = r.Errors.Select(TraduzirErro).ToList() });
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        });
    }

    private static string TraduzirErro(IdentityError e) => e.Code switch
    {
        "PasswordMismatch" => "Senha atual incorreta.",
        "PasswordTooShort" => "A nova senha deve ter pelo menos 10 caracteres.",
        _ => e.Description,
    };
}
