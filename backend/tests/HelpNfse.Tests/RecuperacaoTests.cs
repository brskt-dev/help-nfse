using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores.Simulacao;
using HelpNfse.Tests.Infra;
using HelpNfse.Core.Emissao;
using HelpNfse.Core.Provedores;
using Microsoft.Extensions.DependencyInjection;

namespace HelpNfse.Tests;

/// <summary>Recuperação após interrupção do worker.</summary>
public class RecuperacaoTests(PostgresServidor servidor) : TesteComBanco(servidor)
{
    [Fact]
    public async Task Queda_do_worker_apos_envio_e_reconciliada_por_consulta_sem_reenvio()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.InterromperWorker);
        await A.SolicitarEmissaoAsync(compId);

        // O "servidor" emite e o processo cai antes de registrar a resposta.
        await Assert.ThrowsAsync<InterrupcaoSimuladaException>(() => A.Processador.ProcessarProximoAsync("worker-1", default));
        var aposQueda = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Processando, aposQueda.Situacao);
        Assert.Equal(ResultadoTentativa.EmAndamento, Assert.Single(aposQueda.Tentativas).Resultado);

        // Reinício do mesmo worker: libera seus trabalhos e reconcilia.
        Assert.Equal(1, await A.Processador.RecuperarTrabalhosInterrompidosAsync("worker-1", default));
        await A.ProcessarDisponiveisAsync("worker-1");

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        var tentativas = nota.Tentativas.OrderBy(t => t.Sequencia).ToList();
        Assert.Equal([ResultadoTentativa.Interrompida, ResultadoTentativa.Encontrada], tentativas.Select(t => t.Resultado));
        Assert.Equal(TipoTentativa.Consulta, tentativas[1].Tipo);
        Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(notas[0]));
    }

    [Fact]
    public async Task Trabalho_abandonado_e_assumido_por_outro_worker_apos_expirar_o_bloqueio()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.InterromperWorker);
        await A.SolicitarEmissaoAsync(compId);
        await Assert.ThrowsAsync<InterrupcaoSimuladaException>(() => A.Processador.ProcessarProximoAsync("worker-1", default));

        // Antes de o bloqueio expirar, ninguém assume.
        Assert.Equal(0, await A.ProcessarDisponiveisAsync("worker-2"));

        A.Relogio.Avancar(Ambiente.Bloqueio + TimeSpan.FromSeconds(1));
        await A.ProcessarDisponiveisAsync("worker-2");

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(notas[0]));
        Assert.Contains(nota.Tentativas, t => t.Tipo == TipoTentativa.Consulta && t.WorkerId == "worker-2");
    }

    [Fact]
    public async Task Queda_antes_do_envio_resulta_em_envio_normal_apos_consulta()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1);
        await A.SolicitarEmissaoAsync(compId);
        // Simula queda logo após reivindicar o trabalho (antes de registrar a tentativa).
        var t = await A.Processador.ReivindicarAsync("worker-1", default);
        Assert.NotNull(t);

        A.Relogio.Avancar(Ambiente.Bloqueio + TimeSpan.FromSeconds(1));
        await A.ProcessarDisponiveisAsync("worker-2");

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        Assert.Single(nota.Tentativas);
        Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(notas[0]));
    }

    [Fact]
    public async Task Resultado_registrado_por_worker_que_perdeu_o_bloqueio_nao_gera_reenvio()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1);
        await A.SolicitarEmissaoAsync(compId);
        var lento = await A.Processador.ReivindicarAsync("worker-lento", default);

        // Durante a chamada ao provedor, o bloqueio do worker lento expira e outro worker assume o trabalho.
        ProcessadorEmissao.TrabalhoReivindicado? rapido = null;
        _gancho.AntesDoEnvio = async () =>
        {
            A.Relogio.Avancar(Ambiente.Bloqueio + TimeSpan.FromSeconds(1));
            rapido = await A.Processador.ReivindicarAsync("worker-rapido", default);
        };
        await A.Processador.ProcessarAsync(lento!, "worker-lento", default);
        _gancho.AntesDoEnvio = null;

        // O envio foi autorizado, mas o worker lento não é mais o dono: o resultado fica só na tentativa.
        Assert.NotNull(rapido);
        var intermediaria = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Processando, intermediaria.Situacao);
        Assert.Equal(ResultadoTentativa.Autorizada, Assert.Single(intermediaria.Tentativas).Resultado);

        // O novo dono vê um envio sem resultado aplicado: consulta em vez de reenviar.
        await A.Processador.ProcessarAsync(rapido!, "worker-rapido", default);

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(notas[0]));
        Assert.Equal(TipoTentativa.Consulta, nota.Tentativas.MaxBy(x => x.Sequencia)!.Tipo);
    }

    private readonly ProvedorComGancho.Gancho _gancho = new();

    protected override void ConfigurarServicos(IServiceCollection services)
    {
        services.AddSingleton<ProvedorSimulado>();
        services.AddSingleton(_gancho);
        services.AddSingleton<IProvedorNfse, ProvedorComGancho>();
    }
}
