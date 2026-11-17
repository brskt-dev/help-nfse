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
        Assert.Contains("Informe o valor desta competência.", linha.Pendencias);
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
}
