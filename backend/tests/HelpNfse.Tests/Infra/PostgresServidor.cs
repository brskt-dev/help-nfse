using HelpNfse.Core;
using HelpNfse.Core.Dados;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(HelpNfse.Tests.Infra.PostgresServidor))]

namespace HelpNfse.Tests.Infra;

/// <summary>
/// Servidor PostgreSQL para os testes. Usa HELPNFSE_TEST_PG (connection string de um usuário com CREATEDB)
/// quando definida; caso contrário sobe um container com Testcontainers (requer Docker).
/// Cada teste recebe um banco novo, clonado de um template já migrado.
/// </summary>
public sealed class PostgresServidor : IAsyncLifetime
{
    private const string Template = "helpnfse_teste_template";
    private PostgreSqlContainer? _container;
    private readonly List<string> _bancos = [];
    private string _admin = "";

    public async ValueTask InitializeAsync()
    {
        var env = Environment.GetEnvironmentVariable("HELPNFSE_TEST_PG");
        if (string.IsNullOrWhiteSpace(env))
        {
            _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
            await _container.StartAsync();
            env = _container.GetConnectionString();
        }
        _admin = new NpgsqlConnectionStringBuilder(env) { Pooling = false, Database = "postgres" }.ConnectionString;

        await ExecutarAdminAsync($"DROP DATABASE IF EXISTS {Template} WITH (FORCE)");
        await ExecutarAdminAsync($"CREATE DATABASE {Template}");
        var opts = new DbContextOptionsBuilder<AppDbContext>();
        ConfiguracaoServicos.ConfigurarBanco(opts, ConnectionString(Template));
        await using (var db = new AppDbContext(opts.Options)) await db.Database.MigrateAsync();
        NpgsqlConnection.ClearAllPools();
    }

    public string ConnectionString(string banco) =>
        new NpgsqlConnectionStringBuilder(_admin) { Database = banco, Pooling = banco != Template, MaxPoolSize = 30 }.ConnectionString;

    public async Task<string> CriarBancoAsync()
    {
        var nome = $"helpnfse_t_{Guid.NewGuid():N}"[..40];
        await ExecutarAdminAsync($"CREATE DATABASE {nome} TEMPLATE {Template}");
        lock (_bancos) _bancos.Add(nome);
        return ConnectionString(nome);
    }

    private async Task ExecutarAdminAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_admin);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }
        foreach (var b in _bancos) await ExecutarAdminAsync($"DROP DATABASE IF EXISTS {b} WITH (FORCE)");
        await ExecutarAdminAsync($"DROP DATABASE IF EXISTS {Template} WITH (FORCE)");
    }
}
