using HelpNfse.Core.Configuracao;
using HelpNfse.Core.Emissao;
using HelpNfse.Core.Provedores.Simulacao;
using Microsoft.Extensions.Options;

namespace HelpNfse.Worker;

/// <summary>
/// Consome a fila de emissão gravada no banco. Processa um trabalho por vez, independentemente do navegador.
/// Pode haver mais de uma instância: a reivindicação usa FOR UPDATE SKIP LOCKED e bloqueio com prazo.
/// </summary>
public sealed class ServicoFilaEmissao(
    ProcessadorEmissao processador,
    IOptions<OpcoesEmissao> opcoes,
    IOptions<OpcoesSimulacao> simulacao,
    TimeProvider relogio,
    ILogger<ServicoFilaEmissao> log) : BackgroundService
{
    /// <summary>Arquivo tocado a cada ciclo; usado pelo healthcheck do container.</summary>
    public static readonly string ArquivoSinal = Path.Combine(Path.GetTempPath(), "helpnfse-worker.alive");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = string.IsNullOrWhiteSpace(opcoes.Value.WorkerId) ? $"worker-{Environment.MachineName}" : opcoes.Value.WorkerId!;
        var iniciadoEm = relogio.GetUtcNow();
        log.LogInformation("Worker {WorkerId} iniciado. Provedor: SIMULAÇÃO — SEM VALIDADE FISCAL. Interrupção real habilitada: {Interrupcao}",
            workerId, simulacao.Value.PermitirInterrupcaoReal);

        await AguardarBancoAsync(workerId, iniciadoEm, stoppingToken);
        var recuperados = await processador.RecuperarTrabalhosInterrompidosAsync(workerId, stoppingToken);
        if (recuperados > 0)
            log.LogWarning("{Qtd} trabalho(s) interrompido(s) deste worker serão reconciliados.", recuperados);

        var ultimoSinal = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (relogio.GetUtcNow() - ultimoSinal > TimeSpan.FromSeconds(10))
                {
                    await processador.RegistrarSinalAsync(workerId, iniciadoEm, stoppingToken);
                    ultimoSinal = relogio.GetUtcNow();
                }
                TocarArquivoSinal();

                // Não repassa o stoppingToken ao processamento: uma chamada iniciada termina e tem o resultado gravado.
                var processou = await processador.ProcessarProximoAsync(workerId, CancellationToken.None);
                if (!processou) await Task.Delay(opcoes.Value.IntervaloPolling, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // O trabalho em andamento fica bloqueado até o prazo expirar e então é reconciliado.
                log.LogError(ex, "Falha no ciclo de processamento; nova tentativa em instantes.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        log.LogInformation("Worker {WorkerId} finalizado.", workerId);
    }

    private async Task AguardarBancoAsync(string workerId, DateTimeOffset iniciadoEm, CancellationToken ct)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await processador.RegistrarSinalAsync(workerId, iniciadoEm, ct);
                TocarArquivoSinal();
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning("Banco indisponível ({Erro}). Tentativa {N}.", ex.Message, tentativa);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, tentativa * 2)), ct);
            }
        }
    }

    private static void TocarArquivoSinal()
    {
        try { File.WriteAllText(ArquivoSinal, DateTimeOffset.UtcNow.ToString("O")); }
        catch { /* sem disco gravável: o healthcheck apenas não confirma */ }
    }
}

/// <summary>Encerra o processo imediatamente (como uma queda real), se permitido na configuração.</summary>
public sealed class InterruptorProcessoReal(IOptions<OpcoesSimulacao> opcoes, ILogger<InterruptorProcessoReal> log) : IInterruptorProcesso
{
    public void Interromper()
    {
        if (!opcoes.Value.PermitirInterrupcaoReal) throw new InterrupcaoSimuladaException();
        log.LogCritical("SIMULAÇÃO: encerrando o processo do worker abruptamente (cenário InterromperWorker).");
        System.Diagnostics.Process.GetCurrentProcess().Kill();
        // Como PID 1 de um container sem init, o próprio SIGKILL é ignorado pelo kernel: encerra de outra forma.
        Thread.Sleep(TimeSpan.FromSeconds(2));
        Environment.FailFast("SIMULAÇÃO: interrupção do worker (cenário InterromperWorker).");
    }
}
