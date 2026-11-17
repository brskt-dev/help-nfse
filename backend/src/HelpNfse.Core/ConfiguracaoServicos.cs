using HelpNfse.Core.Configuracao;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Emissao;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Provedores.Simulacao;
using HelpNfse.Core.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HelpNfse.Core;

public static class ConfiguracaoServicos
{
    public const string ConnectionStringApp = "Default";
    public const string ConnectionStringMigracoes = "Migrations";

    public static string ObterConnectionString(IConfiguration config, string nome = ConnectionStringApp) =>
        config.GetConnectionString(nome) is { Length: > 0 } cs
            ? cs
            : throw new InvalidOperationException(
                $"Connection string '{nome}' não configurada (variável ConnectionStrings__{nome}).");

    public static void ConfigurarBanco(DbContextOptionsBuilder o, string connectionString) =>
        o.UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
         .UseSnakeCaseNamingConvention();

    /// <summary>Serviços compartilhados entre API e worker.</summary>
    public static IServiceCollection AddHelpNfseCore(this IServiceCollection services, IConfiguration config)
    {
        var cs = ObterConnectionString(config);
        services.AddDbContextFactory<AppDbContext>(o => ConfigurarBanco(o, cs));

        services.Configure<OpcoesEmissao>(config.GetSection(OpcoesEmissao.Secao));
        services.Configure<OpcoesSimulacao>(config.GetSection(OpcoesSimulacao.Secao));
        services.Configure<OpcoesCertificado>(config.GetSection(OpcoesCertificado.Secao));
        services.Configure<OpcoesDemo>(config.GetSection(OpcoesDemo.Secao));
        services.TryAddSingleton(TimeProvider.System);

        var provedor = config.GetSection(OpcoesEmissao.Secao)["Provedor"] ?? ProvedorSimulado.NomeProvedor;
        if (!string.Equals(provedor, ProvedorSimulado.NomeProvedor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Provedor '{provedor}' não está disponível nesta versão. Somente '{ProvedorSimulado.NomeProvedor}' é suportado; "
                + "a integração com a NFS-e Nacional será uma etapa separada.");

        services.TryAddSingleton<IInterruptorProcesso, InterruptorPorExcecao>();
        services.AddSingleton<IProvedorNfse, ProvedorSimulado>();

        services.AddScoped<EmissorServico>();
        services.AddScoped<RecorrenciaServico>();
        services.AddScoped<CompetenciaServico>();
        services.AddScoped<NotaServico>();
        services.AddScoped<EmissaoServico>();
        services.AddSingleton<ProcessadorEmissao>();
        return services;
    }
}
