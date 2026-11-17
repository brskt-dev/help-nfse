using HelpNfse.Core.Configuracao;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Provedores;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HelpNfse.Api.Infra;

public static class Inicializacao
{
    public static async Task ExecutarAsync(WebApplication app)
    {
        var config = app.Configuration;
        var log = app.Logger;

        if (config.GetValue<bool>("Banco:MigrarAoIniciar"))
        {
            // Conveniência para desenvolvimento local. No deploy, o serviço "migrate" faz isso antes.
            if (await Migracoes.ExecutarAsync(config) != 0) throw new InvalidOperationException("Falha nas migrações.");
        }

        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await GarantirAdministradorAsync(sp, config, log);

        if (sp.GetRequiredService<IOptions<OpcoesDemo>>().Value.CarregarDados)
        {
            var db = sp.GetRequiredService<AppDbContext>();
            if (await SemeadorDemo.SemearAsync(db, sp.GetRequiredService<IProvedorNfse>(), sp.GetRequiredService<TimeProvider>(), default))
                log.LogInformation("Dados fictícios de demonstração carregados.");
        }
    }

    /// <summary>
    /// Cria o administrador único a partir de Admin__Email/Admin__Senha quando não existe nenhum usuário.
    /// Com Admin__RedefinirSenha=true, redefine a senha do administrador (remova a variável depois).
    /// </summary>
    private static async Task GarantirAdministradorAsync(IServiceProvider sp, IConfiguration config, ILogger log)
    {
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var email = config["Admin:Email"];
        var senha = config["Admin:Senha"];
        var existe = users.Users.Any();

        if (!existe)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                log.LogWarning("Nenhum administrador cadastrado. Defina Admin__Email e Admin__Senha para criar o acesso inicial.");
                return;
            }
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var r = await users.CreateAsync(user, senha);
            if (!r.Succeeded)
                throw new InvalidOperationException("Não foi possível criar o administrador: " + string.Join("; ", r.Errors.Select(e => e.Description)));
            log.LogInformation("Administrador inicial criado.");
            return;
        }

        if (config.GetValue<bool>("Admin:RedefinirSenha") && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(senha))
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null) { log.LogWarning("Admin:RedefinirSenha ignorado: usuário não encontrado."); return; }
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var r = await users.ResetPasswordAsync(user, token, senha);
            if (r.Succeeded)
            {
                await users.SetLockoutEndDateAsync(user, null);
                await users.ResetAccessFailedCountAsync(user);
                log.LogWarning("Senha do administrador redefinida por configuração. Remova Admin__RedefinirSenha.");
            }
            else log.LogError("Falha ao redefinir senha: {Erros}", string.Join("; ", r.Errors.Select(e => e.Description)));
        }
    }
}
