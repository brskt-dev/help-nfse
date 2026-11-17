using HelpNfse.Core.Dominio;
using HelpNfse.Core.Servicos;
using HelpNfse.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Tests;

/// <summary>Concorrência e prevenção de processamento duplicado.</summary>
public class EmissaoConcorrenciaTests(PostgresServidor servidor) : TesteComBanco(servidor)
{
    [Fact]
    public async Task Clique_repetido_com_mesma_chave_retorna_o_mesmo_lote()
    {
        var (compId, notas) = await A.PrepararProntasAsync(3);
        var previa = await A.Com<HelpNfse.Core.Emissao.EmissaoServico, PreviaEmissaoDto>(s => s.PreviaAsync(compId, default));
        var pedido = new SolicitarEmissaoDto(Guid.NewGuid(), previa.Itens.Select(i => i.Id).ToList(), previa.ValorTotal);

        // Cinco cliques simultâneos na mesma confirmação.
        var resultados = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
            A.Com<HelpNfse.Core.Emissao.EmissaoServico, LoteDto>(s => s.SolicitarAsync(compId, pedido, "t", default)))));

        Assert.Single(resultados.Select(r => r.Id).Distinct());
        await using var db = A.NovoDb();
        Assert.Equal(1, await db.Lotes.CountAsync());
        Assert.Equal(3, await db.Trabalhos.CountAsync());
    }

    [Fact]
    public async Task Pedidos_concorrentes_com_chaves_diferentes_nao_duplicam_trabalhos()
    {
        var (compId, notas) = await A.PrepararProntasAsync(4);
        var previa = await A.Com<HelpNfse.Core.Emissao.EmissaoServico, PreviaEmissaoDto>(s => s.PreviaAsync(compId, default));

        var tarefas = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            try
            {
                await A.Com<HelpNfse.Core.Emissao.EmissaoServico, LoteDto>(s => s.SolicitarAsync(compId,
                    new SolicitarEmissaoDto(Guid.NewGuid(), previa.Itens.Select(i => i.Id).ToList(), previa.ValorTotal), "t", default));
                return true;
            }
            catch (ErroNegocio e) when (e.Status == 409) { return false; }
        })).ToArray();
        var ok = await Task.WhenAll(tarefas);

        Assert.Equal(1, ok.Count(x => x));
        await using var db = A.NovoDb();
        Assert.Equal(4, await db.Trabalhos.CountAsync());
        Assert.All(await db.Notas.ToListAsync(), n => Assert.Equal(SituacaoNota.Processando, n.Situacao));
    }

    [Fact]
    public async Task Pedido_com_lista_desatualizada_e_recusado()
    {
        var (compId, notas) = await A.PrepararProntasAsync(2);
        var previa = await A.Com<HelpNfse.Core.Emissao.EmissaoServico, PreviaEmissaoDto>(s => s.PreviaAsync(compId, default));
        await A.DefinirValorAsync(notas[0], 555m); // volta para pendente

        var erro = await Assert.ThrowsAsync<ErroNegocio>(() => A.Com<HelpNfse.Core.Emissao.EmissaoServico, LoteDto>(s =>
            s.SolicitarAsync(compId, new SolicitarEmissaoDto(Guid.NewGuid(), previa.Itens.Select(i => i.Id).ToList(), previa.ValorTotal), "t", default)));

        Assert.Equal(409, erro.Status);
        await using var db = A.NovoDb();
        Assert.Equal(0, await db.Trabalhos.CountAsync());
    }

    [Fact]
    public async Task Varios_workers_concorrentes_enviam_cada_nota_uma_unica_vez()
    {
        var (compId, notas) = await A.PrepararProntasAsync(12);
        await A.SolicitarEmissaoAsync(compId);

        var workers = Enumerable.Range(1, 4).Select(w => Task.Run(() => A.ProcessarDisponiveisAsync($"worker-{w}"))).ToArray();
        var processados = await Task.WhenAll(workers);

        Assert.Equal(12, processados.Sum());
        foreach (var id in notas)
        {
            var nota = await A.NotaAsync(id);
            Assert.Equal(SituacaoNota.Emitida, nota.Situacao);
            Assert.Single(nota.Tentativas);
            Assert.Equal(1, await A.ChamadasEnvioSimuladorAsync(id));
        }
    }

    [Fact]
    public async Task Indice_unico_impede_dois_trabalhos_ativos_para_a_mesma_nota()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1);
        await A.SolicitarEmissaoAsync(compId);

        await using var db = A.NovoDb();
        db.Trabalhos.Add(new TrabalhoEmissao
        {
            NotaId = notas[0], Status = StatusTrabalho.Pendente, ProximaExecucaoEm = A.Relogio.GetUtcNow(),
            CriadoEm = A.Relogio.GetUtcNow(), AtualizadoEm = A.Relogio.GetUtcNow(),
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Bloqueio_ativo_nao_e_reivindicado_por_outro_worker()
    {
        var (compId, _) = await A.PrepararProntasAsync(1);
        await A.SolicitarEmissaoAsync(compId);

        var primeiro = await A.Processador.ReivindicarAsync("worker-a", default);
        var segundo = await A.Processador.ReivindicarAsync("worker-b", default);

        Assert.NotNull(primeiro);
        Assert.Null(segundo);
    }

    [Fact]
    public async Task Numero_da_dps_e_atribuido_uma_vez_e_reutilizado_apos_rejeicao()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, cenario: CenarioSimulacao.RejeicaoFiscal);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();
        var rejeitada = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Erro, rejeitada.Situacao);

        await A.DefinirCenarioAsync(notas[0], CenarioSimulacao.Sucesso);
        await A.MarcarProntasAsync(notas[0]);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        var emitida = await A.NotaAsync(notas[0]);
        Assert.Equal(SituacaoNota.Emitida, emitida.Situacao);
        Assert.Equal(rejeitada.NumeroDps, emitida.NumeroDps);
        Assert.Equal(rejeitada.IdentificadorDps, emitida.IdentificadorDps);
        var emissor = await A.Com<EmissorServico, EmissorDto>(s => s.ObterAsync(default));
        Assert.Equal(2, emissor.ProximoNumeroDps);
    }
}
