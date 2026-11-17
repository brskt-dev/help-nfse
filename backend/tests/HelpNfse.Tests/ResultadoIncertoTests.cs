using HelpNfse.Core.Dominio;
using HelpNfse.Core.Emissao;
using HelpNfse.Core.Servicos;
using HelpNfse.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Tests;

/// <summary>Tratamento de resultado incerto antes de uma nova tentativa, e demais desfechos do provedor.</summary>
public class ResultadoIncertoTests(PostgresServidor servidor) : TesteComBanco(servidor)
{
    [Fact]
    public async Task Timeout_com_emissao_efetivada_e_confirmado_por_consulta_sem_reenvio()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.TimeoutEmitida);
        await A.SolicitarEmissaoAsync(compId);

        await A.ProcessarDisponiveisAsync();
        var incerta = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.ResultadoAConfirmar, incerta.Situacao);
        Assert.Equal(ResultadoTentativa.Incerta, Assert.Single(incerta.Tentativas).Resultado);

        // Nada acontece antes do horário da consulta.
        Assert.Equal(0, await A.ProcessarDisponiveisAsync());
        A.Relogio.Avancar(TimeSpan.FromSeconds(31));
        await A.ProcessarDisponiveisAsync();

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        Assert.Equal([ResultadoTentativa.Incerta, ResultadoTentativa.Encontrada],
            nota.Tentativas.OrderBy(t => t.Sequencia).Select(t => t.Resultado));
        Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(notas[0]));
    }

    [Fact]
    public async Task Timeout_sem_emissao_so_reenvia_depois_de_consulta_confirmar_inexistencia_fora_da_janela()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.TimeoutNaoEmitida);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        // Primeira consulta: ainda dentro da janela mínima → não reenvia.
        A.Relogio.Avancar(TimeSpan.FromSeconds(31));
        await A.ProcessarDisponiveisAsync();
        var dentroDaJanela = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.ResultadoAConfirmar, dentroDaJanela.Situacao);
        Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(notas[0]));

        // Após a janela: consulta confirma inexistência e só então reenvia (mesmo identificador).
        A.Relogio.Avancar(Ambiente.JanelaReenvio);
        await A.ProcessarDisponiveisAsync();

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        var seq = nota.Tentativas.OrderBy(t => t.Sequencia).Select(t => (t.Tipo, t.Resultado)).ToList();
        Assert.Equal([
            (TipoTentativa.Envio, ResultadoTentativa.Incerta),
            (TipoTentativa.Consulta, ResultadoTentativa.NaoEncontrada),
            (TipoTentativa.Consulta, ResultadoTentativa.NaoEncontrada),
            (TipoTentativa.Envio, ResultadoTentativa.Autorizada),
        ], seq);
        Assert.Single(nota.Tentativas.Select(t => t.IdentificadorDps).Distinct());
        Assert.Equal(2, await A.ChamadasEnvioSimuladorAsync(notas[0]));
    }

    [Fact]
    public async Task Consulta_indisponivel_mantem_resultado_a_confirmar_e_nunca_reenvia()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.ConsultaIndisponivel);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        for (var i = 0; i < 2; i++)
        {
            A.Relogio.Avancar(TimeSpan.FromMinutes(1));
            await A.ProcessarDisponiveisAsync();
            Assert.Equal(SituacaoNota.ResultadoAConfirmar, (await A.NotaAsync(notas[0])).Situacao);
        }
        A.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await A.ProcessarDisponiveisAsync();

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        Assert.Equal(2, nota.Tentativas.Count(t => t.Resultado == ResultadoTentativa.ConsultaIndisponivel));
        Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(notas[0]));
    }

    [Fact]
    public async Task Consultas_esgotadas_suspendem_e_usuario_pode_pedir_nova_consulta()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.ConsultaIndisponivel);
        await using (var db = A.NovoDb())
            await db.Database.ExecuteSqlAsync($"SELECT 1"); // garante banco pronto
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        // Com MaxConsultasIndisponiveis = 5 e o simulador indisponível só 2 vezes, forçamos indisponibilidade extra.
        for (var i = 0; i < 2; i++)
        {
            A.Relogio.Avancar(TimeSpan.FromMinutes(1));
            await A.ProcessarDisponiveisAsync();
        }
        await using (var db = A.NovoDb())
        {
            var t = await db.Trabalhos.SingleAsync();
            Assert.Equal(StatusTrabalho.Pendente, t.Status);
        }

        // Usuário não pode editar nem marcar como pronta enquanto o resultado é incerto.
        var nota = await A.NotaAsync(notas[0]);
        await Assert.ThrowsAsync<ErroNegocio>(() =>
            A.Com<NotaServico>(s => s.AtualizarValorAsync(nota.Id, new AtualizarValorDto(nota.Versao, 1m), default)));
        Assert.Equal(1, (await A.MarcarProntasAsync(nota.Id)).Falhas);

        // Pedido explícito de consulta é atendido imediatamente.
        await A.Com<NotaServico>(s => s.SolicitarConsultaAsync(nota.Id, default));
        await A.ProcessarDisponiveisAsync();
        Assert.Equal(SituacaoNota.Emitida, (await A.NotaAsync(notas[0])).Situacao);
    }

    [Fact]
    public async Task Falha_temporaria_reenvia_com_espera_e_conclui()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.FalhaTemporaria);
        await A.SolicitarEmissaoAsync(compId);

        for (var i = 0; i < 3; i++)
        {
            await A.ProcessarDisponiveisAsync();
            A.Relogio.Avancar(TimeSpan.FromSeconds(11));
        }

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        Assert.Equal([ResultadoTentativa.FalhaTemporaria, ResultadoTentativa.FalhaTemporaria, ResultadoTentativa.Autorizada],
            nota.Tentativas.OrderBy(t => t.Sequencia).Select(t => t.Resultado));
    }

    [Fact]
    public async Task Falha_temporaria_persistente_vira_erro_conhecido()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.FalhaTemporariaPersistente);
        await A.SolicitarEmissaoAsync(compId);

        for (var i = 0; i < 5; i++)
        {
            await A.ProcessarDisponiveisAsync();
            A.Relogio.Avancar(TimeSpan.FromSeconds(11));
        }

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Erro, nota.Situacao);
        Assert.Equal(3, nota.Tentativas.Count);
        Assert.Contains("Nenhuma nota foi emitida", nota.UltimaMensagem);
    }

    [Fact]
    public async Task Rejeicao_vira_erro_e_pode_ser_corrigida_preservando_o_historico()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.RejeicaoFiscal);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        var rejeitada = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Erro, rejeitada.Situacao);
        Assert.StartsWith("Rejeitada: SIM-E001", rejeitada.UltimaMensagem);
        Assert.Equal("SIM-E001", Assert.Single(Assert.Single(rejeitada.Tentativas).Mensagens).Codigo);

        await A.DefinirCenarioAsync(notas[0], CenarioSimulacao.Sucesso);
        Assert.Equal(SituacaoNota.Pendente, (await A.NotaAsync(notas[0])).Situacao);
        await A.MarcarProntasAsync(notas[0]);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        var nota = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
        Assert.Equal([ResultadoTentativa.Rejeitada, ResultadoTentativa.Autorizada],
            nota.Tentativas.OrderBy(t => t.Sequencia).Select(t => t.Resultado));
    }

    [Fact]
    public void Envio_sem_resultado_conclusivo_exige_reconciliacao()
    {
        Nota NotaCom(params (TipoTentativa, ResultadoTentativa)[] t) => new()
        {
            Tentativas = t.Select((x, i) => new TentativaEmissao { Sequencia = i + 1, Tipo = x.Item1, Resultado = x.Item2 }).ToList(),
        };

        Assert.False(ProcessadorEmissao.PrecisaReconciliar(NotaCom()));
        Assert.False(ProcessadorEmissao.PrecisaReconciliar(NotaCom((TipoTentativa.Envio, ResultadoTentativa.Rejeitada))));
        Assert.False(ProcessadorEmissao.PrecisaReconciliar(NotaCom((TipoTentativa.Envio, ResultadoTentativa.FalhaTemporaria))));
        Assert.True(ProcessadorEmissao.PrecisaReconciliar(NotaCom((TipoTentativa.Envio, ResultadoTentativa.Incerta))));
        Assert.True(ProcessadorEmissao.PrecisaReconciliar(NotaCom((TipoTentativa.Envio, ResultadoTentativa.Interrompida))));
        Assert.True(ProcessadorEmissao.PrecisaReconciliar(NotaCom((TipoTentativa.Envio, ResultadoTentativa.EmAndamento))));
        Assert.True(ProcessadorEmissao.PrecisaReconciliar(NotaCom((TipoTentativa.Envio, ResultadoTentativa.Autorizada))));
        Assert.True(ProcessadorEmissao.PrecisaReconciliar(NotaCom(
            (TipoTentativa.Envio, ResultadoTentativa.Incerta), (TipoTentativa.Consulta, ResultadoTentativa.ConsultaIndisponivel))));
        Assert.False(ProcessadorEmissao.PrecisaReconciliar(NotaCom(
            (TipoTentativa.Envio, ResultadoTentativa.Incerta), (TipoTentativa.Consulta, ResultadoTentativa.NaoEncontrada))));
    }
}
