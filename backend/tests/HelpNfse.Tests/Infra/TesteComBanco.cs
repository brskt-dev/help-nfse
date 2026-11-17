namespace HelpNfse.Tests.Infra;

public abstract class TesteComBanco(PostgresServidor servidor) : IAsyncLifetime
{
    protected Ambiente A { get; private set; } = null!;
    protected virtual Dictionary<string, string?>? Configuracao => null;
    protected virtual void ConfigurarServicos(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }

    public async ValueTask InitializeAsync() => A = await Ambiente.CriarAsync(servidor, Configuracao, ConfigurarServicos);
    public async ValueTask DisposeAsync() => await A.DisposeAsync();
}
