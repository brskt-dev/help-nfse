using HelpNfse.Core.Provedores;
using HelpNfse.Core.Provedores.Simulacao;

namespace HelpNfse.Tests.Infra;

/// <summary>Decora o simulador para executar uma ação no meio da chamada (ex.: expirar o bloqueio).</summary>
public sealed class ProvedorComGancho(ProvedorSimulado interno, ProvedorComGancho.Gancho gancho) : IProvedorNfse
{
    public sealed class Gancho
    {
        public Func<Task>? AntesDoEnvio { get; set; }
    }

    public string Nome => interno.Nome;
    public bool Simulado => interno.Simulado;
    public string GerarIdentificadorDps(PrestadorDps prestador, string serie, long numero) => interno.GerarIdentificadorDps(prestador, serie, numero);

    public async Task<ResultadoEnvio> EmitirAsync(DadosDps dps, ContextoEnvio contexto, CancellationToken ct)
    {
        if (gancho.AntesDoEnvio is { } g) await g();
        return await interno.EmitirAsync(dps, contexto, ct);
    }

    public Task<ResultadoConsulta> ConsultarPorDpsAsync(string identificadorDps, CancellationToken ct) =>
        interno.ConsultarPorDpsAsync(identificadorDps, ct);
}
