using System.Text.Json;
using HelpNfse.Core.Configuracao;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Provedores.Simulacao;
using HelpNfse.Core.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HelpNfse.Core.Emissao;

/// <summary>
/// Processa a fila de emissão (tabela trabalhos_emissao).
///
/// Garantias:
/// <list type="bullet">
/// <item>Um trabalho por vez por nota: reivindicação com FOR UPDATE SKIP LOCKED + bloqueio com prazo (lease) + índice único parcial.</item>
/// <item>A tentativa de envio é gravada ANTES da chamada ao provedor. Se o processo cair, a tentativa fica "em andamento"
///       e a recuperação consulta o provedor antes de qualquer reenvio.</item>
/// <item>Nenhum reenvio acontece enquanto houver um envio anterior com resultado não conclusivo (incerto, interrompido,
///       autorizado não registrado, duplicidade) sem uma consulta posterior que confirme a inexistência da nota.</item>
/// <item>O resultado de cada chamada é sempre registrado na tentativa; a nota e o trabalho só mudam se o worker
///       ainda for o dono do bloqueio.</item>
/// </list>
/// </summary>
public sealed class ProcessadorEmissao(
    IDbContextFactory<AppDbContext> dbFactory,
    IProvedorNfse provedor,
    IOptions<OpcoesEmissao> opcoes,
    TimeProvider relogio,
    ILogger<ProcessadorEmissao> log)
{
    private OpcoesEmissao Op => opcoes.Value;

    public sealed record TrabalhoReivindicado(long Id, long NotaId, Guid Token);

    /// <summary>Reivindica e processa o próximo trabalho disponível. Retorna false se a fila estiver vazia.</summary>
    public async Task<bool> ProcessarProximoAsync(string workerId, CancellationToken ct)
    {
        var t = await ReivindicarAsync(workerId, ct);
        if (t is null) return false;
        await ProcessarAsync(t, workerId, ct);
        return true;
    }

    public async Task<TrabalhoReivindicado?> ReivindicarAsync(string workerId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var agora = relogio.GetUtcNow();
        var token = Guid.NewGuid();
        await db.Database.OpenConnectionAsync(ct);
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var cmd = new NpgsqlCommand("""
            UPDATE trabalhos_emissao t
               SET status = 'EmExecucao', bloqueado_por = @worker, bloqueado_ate = @ate, token_bloqueio = @token,
                   execucoes = t.execucoes + 1, atualizado_em = @agora
             WHERE t.id = (SELECT id FROM trabalhos_emissao
                            WHERE (status = 'Pendente' AND proxima_execucao_em <= @agora)
                               OR (status = 'EmExecucao' AND bloqueado_ate < @agora)
                            ORDER BY proxima_execucao_em, id
                            LIMIT 1
                            FOR UPDATE SKIP LOCKED)
            RETURNING t.id, t.nota_id
            """, conn);
        cmd.Parameters.AddWithValue("worker", workerId);
        cmd.Parameters.AddWithValue("ate", agora + Op.DuracaoBloqueio);
        cmd.Parameters.AddWithValue("token", token);
        cmd.Parameters.AddWithValue("agora", agora);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new TrabalhoReivindicado(r.GetInt64(0), r.GetInt64(1), token);
    }

    /// <summary>
    /// Na inicialização, libera imediatamente os trabalhos que este mesmo worker (mesmo identificador) deixou
    /// em execução ao ser interrompido. As tentativas abertas serão reconciliadas por consulta.
    /// </summary>
    public async Task<int> RecuperarTrabalhosInterrompidosAsync(string workerId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var agora = relogio.GetUtcNow();
        return await db.Database.ExecuteSqlAsync($"""
            UPDATE trabalhos_emissao SET bloqueado_ate = {agora.AddSeconds(-1)}, atualizado_em = {agora},
                   ultima_mensagem = 'Worker reiniciado durante o processamento; resultado será reconciliado.'
             WHERE status = 'EmExecucao' AND bloqueado_por = {workerId}
            """, ct);
    }

    public async Task RegistrarSinalAsync(string workerId, DateTimeOffset iniciadoEm, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var agora = relogio.GetUtcNow();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO status_workers (worker_id, iniciado_em, ultimo_sinal, provedor)
            VALUES ({workerId}, {iniciadoEm}, {agora}, {provedor.Nome})
            ON CONFLICT (worker_id) DO UPDATE SET ultimo_sinal = EXCLUDED.ultimo_sinal,
                iniciado_em = EXCLUDED.iniciado_em, provedor = EXCLUDED.provedor
            """, ct);
    }

    private enum Acao { Nenhuma, Enviar, Consultar }

    public async Task ProcessarAsync(TrabalhoReivindicado t, string workerId, CancellationToken ct)
    {
        // ---- Etapa 1: preparar e registrar a tentativa ANTES de chamar o provedor ----
        Acao acao;
        long tentativaId;
        DadosDps? dps = null;
        CenarioSimulacao cenario;
        string identificador;

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var trabalho = await db.Trabalhos.SingleAsync(x => x.Id == t.Id, ct);
            if (trabalho.TokenBloqueio != t.Token || trabalho.Status != StatusTrabalho.EmExecucao) return;
            var nota = await db.Notas.Include(n => n.Tentativas).SingleAsync(n => n.Id == t.NotaId, ct);
            var agora = relogio.GetUtcNow();

            if (nota.Situacao is not (SituacaoNota.Processando or SituacaoNota.ResultadoAConfirmar) || nota.DpsJson is null)
            {
                Liberar(trabalho, StatusTrabalho.Cancelado, agora, $"Cancelado: nota em situação {nota.Situacao}.");
                await db.SaveChangesAsync(ct);
                return;
            }

            // Tentativas que ficaram abertas pertencem a uma execução interrompida.
            foreach (var aberta in nota.Tentativas.Where(x => x.Resultado == ResultadoTentativa.EmAndamento))
            {
                aberta.FinalizadaEm = agora;
                if (aberta.Tipo == TipoTentativa.Envio)
                {
                    aberta.Resultado = ResultadoTentativa.Interrompida;
                    aberta.Detalhe = "Processamento interrompido antes do registro da resposta. "
                                     + "O resultado será confirmado por consulta antes de qualquer reenvio.";
                }
                else
                {
                    aberta.Resultado = ResultadoTentativa.ConsultaIndisponivel;
                    aberta.Detalhe = "Consulta interrompida antes do registro da resposta.";
                }
            }

            identificador = nota.IdentificadorDps!;
            cenario = nota.CenarioSimulacao;
            acao = nota.Situacao == SituacaoNota.ResultadoAConfirmar || PrecisaReconciliar(nota)
                ? Acao.Consultar : Acao.Enviar;

            if (acao == Acao.Consultar && nota.Situacao != SituacaoNota.ResultadoAConfirmar)
            {
                nota.Situacao = SituacaoNota.ResultadoAConfirmar;
                nota.UltimaMensagem = "Envio anterior sem resultado conclusivo. Consultando antes de qualquer reenvio.";
                nota.AtualizadaEm = agora;
            }

            var tentativa = new TentativaEmissao
            {
                NotaId = nota.Id,
                TrabalhoId = trabalho.Id,
                Sequencia = nota.Tentativas.Count == 0 ? 1 : nota.Tentativas.Max(x => x.Sequencia) + 1,
                Tipo = acao == Acao.Enviar ? TipoTentativa.Envio : TipoTentativa.Consulta,
                Resultado = ResultadoTentativa.EmAndamento,
                IniciadaEm = agora,
                IdentificadorDps = identificador,
                Provedor = provedor.Nome,
                WorkerId = workerId,
                DadosEnviadosJson = acao == Acao.Enviar ? nota.DpsJson : null,
            };
            nota.Tentativas.Add(tentativa);
            if (acao == Acao.Enviar)
            {
                dps = JsonSerializer.Deserialize<DadosDps>(nota.DpsJson, JsonOpcoes.Padrao)!;
                nota.UltimaMensagem = "Enviando ao provedor.";
                nota.AtualizadaEm = agora;
            }
            await db.SaveChangesAsync(ct);
            tentativaId = tentativa.Id;
        }

        // ---- Etapa 2: chamar o provedor (fora de transação) ----
        // O cancelamento do host não interrompe a chamada: queremos registrar a resposta se ela chegar.
        using var cts = new CancellationTokenSource(Op.TimeoutProvedor);
        if (acao == Acao.Enviar)
        {
            ResultadoEnvio resultado;
            try
            {
                resultado = await provedor.EmitirAsync(dps!, new ContextoEnvio(cenario, workerId), cts.Token);
            }
            catch (InterrupcaoSimuladaException)
            {
                throw; // Simula a queda do processo: nada é registrado.
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                resultado = new ResultadoEnvio.Incerto($"Tempo limite de {Op.TimeoutProvedor.TotalSeconds:0}s excedido após o envio.");
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Erro inesperado ao enviar DPS {Dps}", identificador);
                resultado = new ResultadoEnvio.Incerto($"Erro inesperado após o início do envio: {ex.GetType().Name}.");
            }
            await RegistrarEnvioAsync(t, tentativaId, resultado, ct);
        }
        else
        {
            ResultadoConsulta resultado;
            try
            {
                resultado = await provedor.ConsultarPorDpsAsync(identificador, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                resultado = new ResultadoConsulta.Indisponivel("Tempo limite excedido na consulta.");
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Erro inesperado ao consultar DPS {Dps}", identificador);
                resultado = new ResultadoConsulta.Indisponivel($"Erro inesperado na consulta: {ex.GetType().Name}.");
            }
            await RegistrarConsultaAsync(t, tentativaId, resultado, ct);
        }
    }

    /// <summary>
    /// Verdadeiro quando o último envio não tem resultado conclusivo de "não processado" e nenhuma consulta posterior
    /// confirmou a inexistência da nota. Nesse caso é proibido reenviar sem consultar.
    /// </summary>
    public static bool PrecisaReconciliar(Nota nota)
    {
        var ultimoEnvio = nota.Tentativas.Where(x => x.Tipo == TipoTentativa.Envio).MaxBy(x => x.Sequencia);
        if (ultimoEnvio is null) return false;
        if (ultimoEnvio.Resultado is ResultadoTentativa.Rejeitada or ResultadoTentativa.FalhaTemporaria) return false;
        var consultaPosterior = nota.Tentativas
            .Where(x => x.Tipo == TipoTentativa.Consulta && x.Sequencia > ultimoEnvio.Sequencia)
            .MaxBy(x => x.Sequencia);
        return consultaPosterior?.Resultado != ResultadoTentativa.NaoEncontrada;
    }

    private async Task RegistrarEnvioAsync(TrabalhoReivindicado t, long tentativaId, ResultadoEnvio resultado, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var agora = relogio.GetUtcNow();
        var trabalho = await db.Trabalhos.FromSql($"SELECT * FROM trabalhos_emissao WHERE id = {t.Id} FOR UPDATE").SingleAsync(ct);
        var tentativa = await db.Tentativas.SingleAsync(x => x.Id == tentativaId, ct);
        var nota = await db.Notas.SingleAsync(x => x.Id == t.NotaId, ct);
        tentativa.FinalizadaEm = agora;

        // O resultado sempre fica registrado na tentativa (histórico), mesmo que o bloqueio tenha sido perdido.
        switch (resultado)
        {
            case ResultadoEnvio.Autorizada a:
                tentativa.Resultado = ResultadoTentativa.Autorizada;
                tentativa.NumeroNfse = a.Nfse.NumeroNfse;
                tentativa.ChaveAcesso = a.Nfse.ChaveAcesso;
                tentativa.Detalhe = a.Nfse.Simulada ? "Emissão SIMULADA autorizada (sem validade fiscal)." : "NFS-e autorizada.";
                break;
            case ResultadoEnvio.Rejeitada r:
                tentativa.Resultado = ResultadoTentativa.Rejeitada;
                tentativa.Mensagens = r.Mensagens.ToList();
                tentativa.Detalhe = "DPS rejeitada pelo provedor.";
                break;
            case ResultadoEnvio.JaProcessada j:
                tentativa.Resultado = ResultadoTentativa.DuplicidadeDetectada;
                tentativa.Detalhe = j.Detalhe;
                break;
            case ResultadoEnvio.FalhaTemporaria f:
                tentativa.Resultado = ResultadoTentativa.FalhaTemporaria;
                tentativa.Detalhe = f.Detalhe;
                break;
            case ResultadoEnvio.Incerto i:
                tentativa.Resultado = ResultadoTentativa.Incerta;
                tentativa.Detalhe = i.Detalhe;
                break;
        }

        if (!DonoDoBloqueio(trabalho, t))
        {
            log.LogWarning("Bloqueio do trabalho {Trabalho} perdido; resultado registrado apenas na tentativa {Tentativa}.", t.Id, tentativaId);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return;
        }

        switch (resultado)
        {
            case ResultadoEnvio.Autorizada a:
                MarcarEmitida(nota, a.Nfse, agora);
                Liberar(trabalho, StatusTrabalho.Concluido, agora, "Emitida.");
                break;

            case ResultadoEnvio.Rejeitada r:
                nota.Situacao = SituacaoNota.Erro;
                nota.UltimaMensagem = "Rejeitada: " + string.Join(" | ", r.Mensagens.Select(m => $"{m.Codigo} - {m.Descricao}"));
                Liberar(trabalho, StatusTrabalho.Concluido, agora, "Rejeitada.");
                break;

            case ResultadoEnvio.JaProcessada:
                nota.Situacao = SituacaoNota.ResultadoAConfirmar;
                nota.UltimaMensagem = "O provedor informou que a DPS já foi processada. Consultando para confirmar.";
                Reagendar(trabalho, agora, agora, "Duplicidade detectada; consulta agendada.");
                break;

            case ResultadoEnvio.FalhaTemporaria f:
                trabalho.FalhasTemporarias++;
                if (!Op.ReenvioAutomatico)
                {
                    // Reenvio manual: o usuário decide quando tentar de novo.
                    nota.Situacao = SituacaoNota.Erro;
                    nota.UltimaMensagem = $"Falha temporária no serviço ({f.Detalhe}). Nenhuma nota foi emitida. "
                                          + "Para reenviar, marque como pronta e emita novamente.";
                    Liberar(trabalho, StatusTrabalho.Concluido, agora, "Falha temporária; aguardando reenvio manual.");
                }
                else if (trabalho.FalhasTemporarias >= Op.MaxFalhasTemporarias)
                {
                    nota.Situacao = SituacaoNota.Erro;
                    nota.UltimaMensagem = $"Serviço indisponível após {trabalho.FalhasTemporarias} tentativas ({f.Detalhe}). "
                                          + "Nenhuma nota foi emitida. Marque como pronta novamente para tentar outra vez.";
                    Liberar(trabalho, StatusTrabalho.Concluido, agora, "Falhas temporárias esgotadas.");
                }
                else
                {
                    var quando = agora + Espera(Op.EsperasFalhaTemporaria ?? OpcoesEmissao.PadraoEsperasFalhaTemporaria, trabalho.FalhasTemporarias);
                    nota.UltimaMensagem = $"Falha temporária ({f.Detalhe}). Nova tentativa às {Formatos.DataHora(quando)[11..]}.";
                    Reagendar(trabalho, agora, quando, "Falha temporária; novo envio agendado.");
                }
                break;

            case ResultadoEnvio.Incerto i:
                nota.Situacao = SituacaoNota.ResultadoAConfirmar;
                nota.UltimaMensagem = $"Resultado incerto ({i.Detalhe}). A emissão pode ter ocorrido; consulta agendada antes de qualquer reenvio.";
                trabalho.ConsultasIndisponiveis = 0;
                Reagendar(trabalho, agora, agora + Espera(Op.EsperasConsulta ?? OpcoesEmissao.PadraoEsperasConsulta, 1), "Resultado incerto; consulta agendada.");
                break;
        }
        nota.AtualizadaEm = agora;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task RegistrarConsultaAsync(TrabalhoReivindicado t, long tentativaId, ResultadoConsulta resultado, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var agora = relogio.GetUtcNow();
        var trabalho = await db.Trabalhos.FromSql($"SELECT * FROM trabalhos_emissao WHERE id = {t.Id} FOR UPDATE").SingleAsync(ct);
        var nota = await db.Notas.Include(n => n.Tentativas).SingleAsync(x => x.Id == t.NotaId, ct);
        var tentativa = nota.Tentativas.Single(x => x.Id == tentativaId);
        tentativa.FinalizadaEm = agora;

        switch (resultado)
        {
            case ResultadoConsulta.Encontrada e:
                tentativa.Resultado = ResultadoTentativa.Encontrada;
                tentativa.NumeroNfse = e.Nfse.NumeroNfse;
                tentativa.ChaveAcesso = e.Nfse.ChaveAcesso;
                tentativa.Detalhe = "Consulta confirmou a emissão.";
                break;
            case ResultadoConsulta.NaoEncontrada n:
                tentativa.Resultado = ResultadoTentativa.NaoEncontrada;
                tentativa.Detalhe = n.Detalhe;
                break;
            case ResultadoConsulta.Indisponivel i:
                tentativa.Resultado = ResultadoTentativa.ConsultaIndisponivel;
                tentativa.Detalhe = i.Detalhe;
                break;
        }

        if (!DonoDoBloqueio(trabalho, t))
        {
            log.LogWarning("Bloqueio do trabalho {Trabalho} perdido durante consulta.", t.Id);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return;
        }

        var ultimoEnvio = nota.Tentativas.Where(x => x.Tipo == TipoTentativa.Envio).MaxBy(x => x.Sequencia);
        switch (resultado)
        {
            case ResultadoConsulta.Encontrada e:
                MarcarEmitida(nota, e.Nfse, agora);
                nota.UltimaMensagem = "Emissão confirmada por consulta (reconciliação).";
                Liberar(trabalho, StatusTrabalho.Concluido, agora, "Emissão confirmada por consulta.");
                break;

            case ResultadoConsulta.NaoEncontrada when ultimoEnvio?.Resultado == ResultadoTentativa.DuplicidadeDetectada:
                // Contradição: o provedor acusou duplicidade, mas a consulta não encontra a nota. Não reenviar.
                nota.UltimaMensagem = "O provedor acusou duplicidade, mas a consulta não encontrou a nota. "
                                      + "Verifique manualmente antes de qualquer nova tentativa.";
                Liberar(trabalho, StatusTrabalho.Suspenso, agora, "Aguardando verificação manual.");
                break;

            case ResultadoConsulta.NaoEncontrada:
                var liberadoEm = (ultimoEnvio?.IniciadaEm ?? agora) + Op.JanelaMinimaReenvio;
                if (tentativa.IniciadaEm >= liberadoEm && !Op.ReenvioAutomatico)
                {
                    // Inexistência confirmada: nada foi emitido. Reenvio manual (mesmo identificador de DPS).
                    nota.Situacao = SituacaoNota.Erro;
                    nota.UltimaMensagem = "A consulta confirmou que a nota NÃO foi emitida. Para reenviar, marque como pronta "
                                          + "e emita novamente (o mesmo número de DPS será usado).";
                    Liberar(trabalho, StatusTrabalho.Concluido, agora, "Inexistência confirmada; aguardando reenvio manual.");
                }
                else if (tentativa.IniciadaEm >= liberadoEm)
                {
                    // Inexistência confirmada fora da janela de processamento: reenvio seguro com o mesmo identificador.
                    nota.Situacao = SituacaoNota.Processando;
                    nota.UltimaMensagem = "Consulta confirmou que a nota não foi emitida. Reenviando com o mesmo identificador de DPS.";
                    Reagendar(trabalho, agora, agora, "Inexistência confirmada; reenvio agendado.");
                }
                else
                {
                    nota.UltimaMensagem = $"Nota ainda não encontrada. Nova consulta às {Formatos.DataHora(liberadoEm)[11..]} antes de decidir pelo reenvio.";
                    Reagendar(trabalho, agora, liberadoEm, "Aguardando janela mínima antes do reenvio.");
                }
                break;

            case ResultadoConsulta.Indisponivel i:
                trabalho.ConsultasIndisponiveis++;
                if (trabalho.ConsultasIndisponiveis >= Op.MaxConsultasIndisponiveis)
                {
                    nota.UltimaMensagem = $"Não foi possível confirmar o resultado após {trabalho.ConsultasIndisponiveis} consultas ({i.Detalhe}). "
                                          + "Use \"Consultar novamente\". A nota NÃO será reenviada sem confirmação.";
                    Liberar(trabalho, StatusTrabalho.Suspenso, agora, "Consultas esgotadas; aguardando ação do usuário.");
                }
                else
                {
                    var quando = agora + Espera(Op.EsperasConsulta ?? OpcoesEmissao.PadraoEsperasConsulta, trabalho.ConsultasIndisponiveis + 1);
                    nota.UltimaMensagem = $"Consulta indisponível ({i.Detalhe}). Nova consulta às {Formatos.DataHora(quando)[11..]}.";
                    Reagendar(trabalho, agora, quando, "Consulta indisponível; nova consulta agendada.");
                }
                break;
        }
        nota.AtualizadaEm = agora;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static bool DonoDoBloqueio(TrabalhoEmissao trabalho, TrabalhoReivindicado t) =>
        trabalho.TokenBloqueio == t.Token && trabalho.Status == StatusTrabalho.EmExecucao;

    private static void MarcarEmitida(Nota nota, NfseAutorizada nfse, DateTimeOffset agora)
    {
        nota.Situacao = SituacaoNota.Emitida;
        nota.NumeroNfse = nfse.NumeroNfse;
        nota.ChaveAcesso = nfse.ChaveAcesso;
        nota.EmitidaEm = nfse.EmitidaEm;
        nota.EmissaoSimulada = nfse.Simulada;
        nota.XmlAutorizado = nfse.Simulada ? null : nfse.XmlAutorizado;
        nota.UltimaMensagem = nfse.Simulada ? "Emitida (SIMULAÇÃO — sem validade fiscal)." : "Emitida.";
        nota.AtualizadaEm = agora;
    }

    private static void Liberar(TrabalhoEmissao t, StatusTrabalho status, DateTimeOffset agora, string mensagem)
    {
        t.Status = status;
        t.BloqueadoPor = null;
        t.BloqueadoAte = null;
        t.TokenBloqueio = null;
        t.UltimaMensagem = mensagem;
        t.AtualizadoEm = agora;
        if (status is StatusTrabalho.Concluido or StatusTrabalho.Cancelado) t.ConcluidoEm = agora;
    }

    private static void Reagendar(TrabalhoEmissao t, DateTimeOffset agora, DateTimeOffset quando, string mensagem)
    {
        Liberar(t, StatusTrabalho.Pendente, agora, mensagem);
        t.ProximaExecucaoEm = quando;
    }

    private static TimeSpan Espera(TimeSpan[] esperas, int tentativa) =>
        esperas.Length == 0 ? TimeSpan.FromSeconds(30) : esperas[Math.Clamp(tentativa - 1, 0, esperas.Length - 1)];
}
