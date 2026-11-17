using HelpNfse.Core.Configuracao;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelpNfse.Core.Provedores.Simulacao;

/// <summary>Lançada para simular a queda do processo durante o envio (usada em testes).</summary>
public sealed class InterrupcaoSimuladaException() : Exception("Interrupção simulada do worker durante o envio.");

/// <summary>Encerra o processo atual. No worker real, mata o processo; em testes, lança <see cref="InterrupcaoSimuladaException"/>.</summary>
public interface IInterruptorProcesso
{
    void Interromper();
}

public sealed class InterruptorPorExcecao : IInterruptorProcesso
{
    public void Interromper() => throw new InterrupcaoSimuladaException();
}

/// <summary>
/// Provedor SIMULADO, sem validade fiscal. Mantém em <see cref="SimuladorRegistro"/> o estado do "servidor remoto",
/// permitindo reproduzir de forma determinística: sucesso, rejeição, falha temporária, timeout com e sem emissão,
/// consulta indisponível e queda do worker durante o envio.
/// O cenário é escolhido por linha da competência (campo "Cenário de simulação").
/// </summary>
public sealed class ProvedorSimulado(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<OpcoesSimulacao> opcoes,
    IInterruptorProcesso interruptor,
    TimeProvider relogio,
    ILogger<ProvedorSimulado> log) : IProvedorNfse
{
    public const string NomeProvedor = "Simulacao";
    public const int FalhasNoCenarioFalhaTemporaria = 1;
    public const int ConsultasIndisponiveisNoCenario = 2;

    public string Nome => NomeProvedor;
    public bool Simulado => true;

    public string GerarIdentificadorDps(PrestadorDps prestador, string serie, long numero) =>
        $"SIM-{prestador.Cnpj}-{serie.PadLeft(5, '0')}-{numero:D15}";

    public async Task<ResultadoEnvio> EmitirAsync(DadosDps dps, ContextoEnvio contexto, CancellationToken ct)
    {
        await Latencia(ct);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var agora = relogio.GetUtcNow();
        var reg = await db.SimuladorRegistros.SingleOrDefaultAsync(x => x.IdentificadorDps == dps.IdentificadorDps, ct);
        if (reg is null)
        {
            reg = new SimuladorRegistro { IdentificadorDps = dps.IdentificadorDps, CriadoEm = agora };
            db.SimuladorRegistros.Add(reg);
        }
        reg.Cenario = contexto.CenarioSimulacao;
        reg.ChamadasEnvio++;
        reg.AtualizadoEm = agora;

        if (reg.Emitida)
        {
            await db.SaveChangesAsync(ct);
            return new ResultadoEnvio.JaProcessada("SIMULAÇÃO: DPS já processada anteriormente (duplicidade).");
        }

        ResultadoEnvio resultado;
        switch (contexto.CenarioSimulacao)
        {
            case CenarioSimulacao.RejeicaoFiscal:
                resultado = new ResultadoEnvio.Rejeitada([
                    new MensagemProvedor("SIM-E001", "Rejeição SIMULADA: código de tributação incompatível com o município de prestação.",
                        "Altere o cenário de simulação para \"Sucesso\" e prepare a nota novamente para simular a correção."),
                ]);
                break;

            case CenarioSimulacao.FalhaTemporaria when reg.FalhasGeradas < FalhasNoCenarioFalhaTemporaria:
            case CenarioSimulacao.FalhaTemporariaPersistente:
                reg.FalhasGeradas++;
                resultado = new ResultadoEnvio.FalhaTemporaria("SIMULAÇÃO: serviço indisponível (HTTP 503 antes do processamento).");
                break;

            case CenarioSimulacao.TimeoutEmitida:
            case CenarioSimulacao.ConsultaIndisponivel:
                // O "servidor" emite, mas a resposta não chega ao cliente.
                await Emitir(db, reg, agora, ct);
                resultado = new ResultadoEnvio.Incerto("SIMULAÇÃO: tempo limite excedido após o envio.");
                break;

            case CenarioSimulacao.TimeoutNaoEmitida when reg.ChamadasEnvio == 1:
                // A requisição se perde: nada é emitido, mas o cliente não sabe disso.
                resultado = new ResultadoEnvio.Incerto("SIMULAÇÃO: tempo limite excedido após o envio.");
                break;

            case CenarioSimulacao.InterromperWorker when !reg.InterrupcaoExecutada:
                await Emitir(db, reg, agora, ct);
                reg.InterrupcaoExecutada = true;
                await db.SaveChangesAsync(ct);
                log.LogWarning("SIMULAÇÃO: interrompendo o worker após emitir a DPS {Dps}.", dps.IdentificadorDps);
                interruptor.Interromper();
                throw new InterrupcaoSimuladaException();

            default:
                await Emitir(db, reg, agora, ct);
                resultado = new ResultadoEnvio.Autorizada(ParaNfse(reg));
                break;
        }

        await db.SaveChangesAsync(ct);
        return resultado;
    }

    public async Task<ResultadoConsulta> ConsultarPorDpsAsync(string identificadorDps, CancellationToken ct)
    {
        await Latencia(ct);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var reg = await db.SimuladorRegistros.SingleOrDefaultAsync(x => x.IdentificadorDps == identificadorDps, ct);
        if (reg is null) return new ResultadoConsulta.NaoEncontrada("SIMULAÇÃO: nenhuma NFS-e para esta DPS.");

        reg.ChamadasConsulta++;
        reg.AtualizadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);

        if (reg.Cenario == CenarioSimulacao.ConsultaIndisponivel && reg.ChamadasConsulta <= ConsultasIndisponiveisNoCenario)
            return new ResultadoConsulta.Indisponivel("SIMULAÇÃO: serviço de consulta indisponível.");

        return reg.Emitida
            ? new ResultadoConsulta.Encontrada(ParaNfse(reg))
            : new ResultadoConsulta.NaoEncontrada("SIMULAÇÃO: nenhuma NFS-e para esta DPS.");
    }

    private async Task Emitir(AppDbContext db, SimuladorRegistro reg, DateTimeOffset agora, CancellationToken ct)
    {
        if (reg.Emitida) return;
        var numero = await db.Database.SqlQuery<long>($"SELECT nextval('simulador_numero_nfse') AS \"Value\"").SingleAsync(ct);
        reg.Emitida = true;
        reg.NumeroNfse = $"SIM-{numero:D6}";
        reg.ChaveAcesso = $"SIMULACAO-SEM-VALIDADE-{Guid.NewGuid():N}".ToUpperInvariant();
        reg.EmitidaEm = agora;
        await db.SaveChangesAsync(ct);
    }

    private static NfseAutorizada ParaNfse(SimuladorRegistro reg) =>
        new(reg.NumeroNfse!, reg.ChaveAcesso!, reg.EmitidaEm!.Value, XmlAutorizado: null, Simulada: true);

    private Task Latencia(CancellationToken ct) =>
        opcoes.Value.Latencia > TimeSpan.Zero ? Task.Delay(opcoes.Value.Latencia, relogio, ct) : Task.CompletedTask;
}
