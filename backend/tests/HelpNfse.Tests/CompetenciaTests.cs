using HelpNfse.Core.Dominio;
using HelpNfse.Core.Servicos;
using HelpNfse.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Tests;

/// <summary>Geração idempotente de competência.</summary>
public class CompetenciaTests(PostgresServidor servidor) : TesteComBanco(servidor)
{
    [Fact]
    public async Task Abrir_duas_vezes_nao_duplica_linhas()
    {
        await A.CriarRecorrenciaAsync("A");
        await A.CriarRecorrenciaAsync("B");

        var primeira = await A.AbrirAsync();
        var segunda = await A.AbrirAsync();

        Assert.Equal(2, primeira.Linhas.Count);
        Assert.Equal(primeira.Linhas.Select(l => l.Id).Order(), segunda.Linhas.Select(l => l.Id).Order());
    }

    [Fact]
    public async Task Abrir_concorrentemente_nao_duplica_linhas()
    {
        for (var i = 0; i < 5; i++) await A.CriarRecorrenciaAsync($"C{i}");

        var tarefas = Enumerable.Range(0, 10).Select(_ => Task.Run(() => A.AbrirAsync())).ToArray();
        await Task.WhenAll(tarefas);

        await using var db = A.NovoDb();
        Assert.Equal(1, await db.Competencias.CountAsync());
        Assert.Equal(5, await db.Notas.CountAsync());
    }

    [Fact]
    public async Task Repetir_abertura_inclui_apenas_recorrencias_novas()
    {
        await A.CriarRecorrenciaAsync("A");
        var antes = await A.AbrirAsync();
        await A.DefinirValorAsync(antes.Linhas[0].Id, 123m);

        await A.CriarRecorrenciaAsync("B");
        var depois = await A.AbrirAsync();

        Assert.Equal(2, depois.Linhas.Count);
        Assert.Equal(123m, depois.Linhas.Single(l => l.Id == antes.Linhas[0].Id).Valor);
    }

    [Fact]
    public async Task Linhas_novas_nascem_pendentes_sem_copiar_valor_anterior()
    {
        await A.CriarEmissorAsync();
        await A.CriarRecorrenciaAsync("A");
        var outubro = await A.AbrirAsync(2026, 10);
        await A.DefinirValorAsync(outubro.Linhas[0].Id, 500m);

        var novembro = await A.AbrirAsync(2026, 11);
        var linha = Assert.Single(novembro.Linhas);

        Assert.Null(linha.Valor);
        Assert.Equal(SituacaoNota.Pendente, linha.Situacao);
        Assert.Equal(500m, linha.ValorAnterior?.Valor);
        Assert.Equal(10, linha.ValorAnterior?.Mes);
        Assert.Contains("Informe o valor deste mês.", linha.Pendencias);
    }

    [Fact]
    public async Task Recorrencia_inativa_nao_gera_linha()
    {
        var a = await A.CriarRecorrenciaAsync("Ativa");
        var b = await A.CriarRecorrenciaAsync("Inativa");
        await A.Com<RecorrenciaServico>(s => s.DefinirAtivaAsync(b.Id, false, default));

        var comp = await A.AbrirAsync();

        Assert.Equal(a.Id, Assert.Single(comp.Linhas).RecorrenciaId);
    }

    [Fact]
    public async Task Descricao_aplica_marcadores_da_competencia()
    {
        await A.CriarRecorrenciaAsync("A", descricao: "Serviços de {mes_ano} (ref. {competencia})");
        var comp = await A.AbrirAsync(2026, 3);
        Assert.Equal("Serviços de março/2026 (ref. 03/2026)", comp.Linhas[0].Descricao);
    }

    [Theory]
    [InlineData("MONITORAMENTO — MÊS DE REFERÊNCIA {MES_REFERENCIA}", 2026, 11, 2026, 10, "MONITORAMENTO — MÊS DE REFERÊNCIA OUTUBRO")]
    [InlineData("Serviço de {mes_ano_referencia} ({referencia}), emitido em {competencia}", 2026, 1, 2025, 12, "Serviço de dezembro/2025 (12/2025), emitido em 01/2026")]
    [InlineData("Referente a {mes_anterior} ({competencia_anterior})", 2026, 1, 2025, 12, "Referente a dezembro (12/2025)")]
    [InlineData("Sem marcadores", 2026, 5, 2026, 4, "Sem marcadores")]
    [InlineData("{Mes} {ano}", 2026, 5, 2026, 4, "maio 2026")]
    public void Marcadores_de_emissao_e_referencia(string modelo, int ano, int mes, int anoRef, int mesRef, string esperado) =>
        Assert.Equal(esperado, HelpNfse.Core.Servicos.ModeloDescricao.Aplicar(modelo, ano, mes, new DateOnly(anoRef, mesRef, 1)));

    [Fact]
    public async Task Mes_de_referencia_do_servico_vem_da_recorrencia_e_e_independente_do_mes_de_emissao()
    {
        await A.CriarRecorrenciaAsync("Anterior", descricao: "Ref. {MES_REFERENCIA}");
        await A.CriarRecorrenciaAsync("Mesmo", descricao: "Ref. {referencia}", referencia: ReferenciaServico.MesmoMes);

        var nov = await A.AbrirAsync(2026, 11);

        var anterior = nov.Linhas.Single(l => l.TomadorNome == "Anterior");
        Assert.Equal(new DateOnly(2026, 10, 1), anterior.MesReferencia);
        Assert.Equal("Ref. OUTUBRO", anterior.Descricao);
        var mesmo = nov.Linhas.Single(l => l.TomadorNome == "Mesmo");
        Assert.Equal(new DateOnly(2026, 11, 1), mesmo.MesReferencia);
        Assert.Equal("Ref. 11/2026", mesmo.Descricao);
    }

    [Fact]
    public async Task Alterar_mes_de_referencia_recalcula_a_descricao_da_linha()
    {
        await A.CriarRecorrenciaAsync("A", CamposVariaveis.Nenhum, "Monitoramento — mês de referência {mes_referencia}");
        var comp = await A.AbrirAsync(2026, 11);
        var nota = await A.NotaAsync(comp.Linhas[0].Id);

        var d = await A.Com<NotaServico, NotaDetalheDto>(s => s.AtualizarAsync(nota.Id,
            new AtualizarNotaDto(nota.Versao, 10m, new DateOnly(2026, 8, 1), null, null, null, null, null, null), default));

        Assert.Equal(new DateOnly(2026, 8, 1), d.MesReferencia);
        Assert.Equal("Monitoramento — mês de referência agosto", d.Servico.Descricao);
        // O mês de emissão continua o mesmo.
        Assert.Equal((2026, 11), (d.Ano, d.Mes));
    }
}
