using System.Text.Json;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Servicos;
using HelpNfse.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Tests;

/// <summary>Preservação dos dados históricos.</summary>
public class HistoricoTests(PostgresServidor servidor) : TesteComBanco(servidor)
{
    [Fact]
    public async Task Alterar_recorrencia_nao_altera_linhas_existentes()
    {
        var rec = await A.CriarRecorrenciaAsync("Cliente Original", descricao: "Descrição original");
        var comp = await A.AbrirAsync();

        await A.Com<RecorrenciaServico>(s => s.AtualizarAsync(rec.Id, new SalvarRecorrenciaDto(
            rec.Identificacao, rec.Tomador with { Nome = "Cliente Renomeado" },
            rec.Servico with { Descricao = "Descrição nova" }, rec.CamposVariaveis, null, rec.Versao), default));

        var linha = (await A.ObterAsync()).Linhas.Single(l => l.Id == comp.Linhas[0].Id);
        Assert.Equal("Cliente Original", linha.TomadorNome);
        Assert.Equal("Descrição original", linha.Descricao);
    }

    [Fact]
    public async Task Atualizar_do_cadastro_so_acontece_de_forma_explicita()
    {
        var rec = await A.CriarRecorrenciaAsync("Cliente Original");
        var comp = await A.AbrirAsync();
        await A.Com<RecorrenciaServico>(s => s.AtualizarAsync(rec.Id, new SalvarRecorrenciaDto(
            rec.Identificacao, rec.Tomador with { Nome = "Cliente Renomeado" }, rec.Servico, rec.CamposVariaveis, null, null), default));

        var detalhe = await A.Com<NotaServico, NotaDetalheDto>(s => s.AtualizarDoCadastroAsync(comp.Linhas[0].Id, default));

        Assert.Equal("Cliente Renomeado", detalhe.Tomador.Nome);
    }

    [Fact]
    public async Task Desativar_recorrencia_preserva_historico()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();
        var nota = await A.NotaAsync(notas[0]);

        await A.Com<RecorrenciaServico>(s => s.DefinirAtivaAsync(nota.RecorrenciaId!.Value, false, default));

        var rec = await A.Com<RecorrenciaServico, RecorrenciaDto>(s => s.ObterAsync(nota.RecorrenciaId!.Value, default));
        Assert.False(rec.Ativa);
        var h = Assert.Single(rec.Historico);
        Assert.Equal(SituacaoNota.Emitida, h.Situacao);
        Assert.Equal(SituacaoNota.Emitida, (await A.ObterAsync()).Linhas.Single().Situacao);
    }

    [Fact]
    public async Task Nota_emitida_mantem_dados_congelados_apos_alterar_emissor_e_cadastro()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1, valor: 321.45m);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        var emissor = await A.Com<EmissorServico, EmissorDto>(s => s.ObterAsync(default));
        await A.Com<EmissorServico>(s => s.SalvarAsync(new SalvarEmissorDto("Nova Razão Social", null, emissor.Cnpj,
            emissor.InscricaoMunicipal, null, null, emissor.Endereco, emissor.OpcaoSimplesNacional, emissor.RegimeApuracaoSimples,
            emissor.RegimeEspecialTributacao, emissor.SerieDps, emissor.ProximoNumeroDps, emissor.RegraDataCompetencia), default));
        var nota = await A.NotaAsync(notas[0]);
        var rec = await A.Com<RecorrenciaServico, RecorrenciaDto>(s => s.ObterAsync(nota.RecorrenciaId!.Value, default));
        await A.Com<RecorrenciaServico>(s => s.AtualizarAsync(rec.Id, new SalvarRecorrenciaDto(rec.Identificacao,
            rec.Tomador with { Nome = "Outro Nome" }, rec.Servico, rec.CamposVariaveis, null, null), default));

        var dps = JsonSerializer.Deserialize<DadosDps>((await A.NotaAsync(notas[0])).DpsJson!, JsonOpcoes.Padrao)!;
        Assert.Equal("Prestador Teste Ltda", dps.Prestador.RazaoSocial);
        Assert.Equal("Cliente 00", dps.Tomador.Nome);
        Assert.Equal(321.45m, dps.Valor);

        var pdf = HelpNfse.Core.Documentos.GeradorPdf.GerarSimulacao(nota, nota.Tentativas, A.Relogio.GetUtcNow());
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public async Task Nota_emitida_fica_bloqueada_para_edicao()
    {
        var (compId, notas) = await A.PrepararProntasAsync(1);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();
        var nota = await A.NotaAsync(notas[0]);

        var erro = await Assert.ThrowsAsync<ErroNegocio>(() =>
            A.Com<NotaServico>(s => s.AtualizarValorAsync(nota.Id, new AtualizarValorDto(nota.Versao, 999m), default)));
        Assert.Equal(409, erro.Status);
        var ignorar = await A.Com<NotaServico, ResultadoLoteDto>(s => s.IgnorarAsync(new IgnorarDto([nota.Id], null), default));
        Assert.Equal(1, ignorar.Falhas);
        Assert.Equal(SituacaoNota.Emitida, (await A.NotaAsync(nota.Id)).Situacao);
    }

    [Fact]
    public async Task Valor_anterior_usa_ultima_competencia_com_valor_e_ignora_linhas_ignoradas()
    {
        await A.CriarRecorrenciaAsync("A");
        var ago = await A.AbrirAsync(2026, 8);
        await A.DefinirValorAsync(ago.Linhas[0].Id, 100m);
        var set = await A.AbrirAsync(2026, 9);
        await A.DefinirValorAsync(set.Linhas[0].Id, 200m);
        await A.Com<NotaServico>(s => s.IgnorarAsync(new IgnorarDto([set.Linhas[0].Id], "férias"), default));
        await A.AbrirAsync(2026, 10);

        var nov = await A.AbrirAsync(2026, 11);

        Assert.Equal(100m, nov.Linhas[0].ValorAnterior?.Valor);
        Assert.Equal(8, nov.Linhas[0].ValorAnterior?.Mes);
    }

    [Fact]
    public async Task Excluir_avulsa_com_tentativas_e_proibido()
    {
        var (compId, _) = await A.PrepararProntasAsync(1);
        var avulsa = await A.Com<NotaServico, NotaDetalheDto>(s => s.CriarAvulsaAsync(compId, new CriarAvulsaDto("Avulsa",
            new DadosTomador { Documento = Ambiente.CnpjValido(), Nome = "Tomador Avulso" },
            new DadosServico { Descricao = "Serviço avulso", CodigoTributacaoNacional = "010101", CodigoMunicipioPrestacao = "3550308" },
            50m, null), default));
        await A.MarcarProntasAsync(avulsa.Id);
        await A.SolicitarEmissaoAsync(compId);
        await A.ProcessarDisponiveisAsync();

        var erro = await Assert.ThrowsAsync<ErroNegocio>(() => A.Com<NotaServico>(s => s.ExcluirAvulsaAsync(avulsa.Id, default)));
        Assert.Equal(409, erro.Status);
        await using var db = A.NovoDb();
        Assert.True(await db.Notas.AnyAsync(n => n.Id == avulsa.Id));
    }
}
