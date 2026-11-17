using HelpNfse.Core;
using HelpNfse.Core.Dados;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Api.Infra;

public static class Migracoes
{
    /// <summary>
    /// Aplica as migrações usando a connection string "Migrations" (usuário dono do schema).
    /// Em desenvolvimento, sem ela, usa "Default".
    /// </summary>
    public static async Task<int> ExecutarAsync(IConfiguration config)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
        var log = loggerFactory.CreateLogger("Migracoes");
        try
        {
            var cs = config.GetConnectionString(ConfiguracaoServicos.ConnectionStringMigracoes) is { Length: > 0 } m
                ? m
                : ConfiguracaoServicos.ObterConnectionString(config);
            var opts = new DbContextOptionsBuilder<AppDbContext>();
            ConfiguracaoServicos.ConfigurarBanco(opts, cs);
            await using var db = new AppDbContext(opts.Options);
            var pendentes = (await db.Database.GetPendingMigrationsAsync()).ToList();
            log.LogInformation("Migrações pendentes: {Qtd} {Lista}", pendentes.Count, string.Join(", ", pendentes));
            await db.Database.MigrateAsync();
            log.LogInformation("Banco atualizado.");
            return 0;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha ao aplicar migrações.");
            return 1;
        }
    }
}
