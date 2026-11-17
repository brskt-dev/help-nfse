using HelpNfse.Core.Dominio;
using HelpNfse.Core.Servicos;
using HelpNfse.Core.Validacao;
using HelpNfse.Tests.Infra;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Tests;

/// <summary>Validação antes de marcar como pronta.</summary>
public class ValidacaoTests(PostgresServidor servidor) : TesteComBanco(servidor)
{
    [Fact]
    public async Task Linha_sem_valor_nao_pode_ficar_pronta()
    {
        await A.CriarRecorrenciaAsync("A");
        var comp = await A.AbrirAsync();

        var r = await A.MarcarProntasAsync(comp.Linhas[0].Id);

        Assert.Equal(1, r.Falhas);
        Assert.Contains("Informe o valor desta competência.", r.Itens[0].Erros);
        Assert.Equal(SituacaoNota.Pendente, (await A.NotaAsync(comp.Linhas[0].Id)).Situacao);
    }

    [Fact]
    public async Task Campo_variavel_precisa_ser_revisado_na_competencia()
    {
        await A.CriarRecorrenciaAsync("A", CamposVariaveis.Descricao, "Serviço de {mes_ano}");
        var comp = await A.AbrirAsync();
        var id = comp.Linhas[0].Id;
        await A.DefinirValorAsync(id, 10m);

        var antes = await A.MarcarProntasAsync(id);
        Assert.Contains(antes.Itens[0].Erros, e => e.Contains("Descrição do serviço"));

        var nota = await A.NotaAsync(id);
        await A.Com<NotaServico>(s => s.AtualizarAsync(id,
            new AtualizarNotaDto(nota.Versao, 10m, "Serviço de novembro/2026 — revisado", null, null, null, null, null), default));
        var depois = await A.MarcarProntasAsync(id);

        Assert.Equal(1, depois.Sucessos);
        Assert.Equal(SituacaoNota.Pronta, (await A.NotaAsync(id)).Situacao);
    }

    [Fact]
    public async Task Campo_nao_variavel_nao_pode_ser_editado_na_linha_de_recorrencia()
    {
        await A.CriarRecorrenciaAsync("A", CamposVariaveis.Nenhum, "Descrição fixa");
        var comp = await A.AbrirAsync();
        var nota = await A.NotaAsync(comp.Linhas[0].Id);

        await A.Com<NotaServico>(s => s.AtualizarAsync(nota.Id,
            new AtualizarNotaDto(nota.Versao, 10m, "Tentativa de alterar", null, null, null, null, null), default));

        Assert.Equal("Descrição fixa", (await A.NotaAsync(nota.Id)).Servico.Descricao);
    }

    [Fact]
    public async Task Lote_misto_marca_apenas_linhas_validas()
    {
        await A.CriarRecorrenciaAsync("Com valor");
        await A.CriarRecorrenciaAsync("Sem valor");
        await A.CriarRecorrenciaAsync("Valor negativo");
        var comp = await A.AbrirAsync();
        var comValor = comp.Linhas.Single(l => l.TomadorNome == "Com valor").Id;
        var semValor = comp.Linhas.Single(l => l.TomadorNome == "Sem valor").Id;
        await A.DefinirValorAsync(comValor, 10m);

        var r = await A.MarcarProntasAsync(comValor, semValor);

        Assert.Equal(1, r.Sucessos);
        Assert.Equal(SituacaoNota.Pronta, (await A.NotaAsync(comValor)).Situacao);
        Assert.Equal(SituacaoNota.Pendente, (await A.NotaAsync(semValor)).Situacao);
        var negativo = comp.Linhas.Single(l => l.TomadorNome == "Valor negativo").Id;
        await Assert.ThrowsAsync<ErroNegocio>(() => A.DefinirValorAsync(negativo, -5m));
    }

    [Fact]
    public async Task Editar_linha_pronta_volta_para_pendente()
    {
        var (_, notas) = await A.PrepararProntasAsync(1);

        await A.DefinirValorAsync(notas[0], 999m);

        Assert.Equal(SituacaoNota.Pendente, (await A.NotaAsync(notas[0])).Situacao);
    }

    [Fact]
    public async Task Tomador_com_documento_invalido_nao_fica_pronto()
    {
        await A.CriarEmissorAsync();
        await A.CriarRecorrenciaAsync("A");
        var comp = await A.AbrirAsync();
        await using (var db = A.NovoDb())
        {
            // Simula dado legado inválido na cópia da linha.
            await db.Database.ExecuteSqlAsync($"UPDATE notas SET tomador_documento = '11111111111111'");
        }
        await A.DefinirValorAsync(comp.Linhas[0].Id, 10m);

        var r = await A.MarcarProntasAsync(comp.Linhas[0].Id);
        Assert.Contains("CPF/CNPJ do tomador inválido.", r.Itens[0].Erros);
    }

    [Fact]
    public async Task Copiar_valor_anterior_e_explicito_e_nao_sobrescreve_sem_pedido()
    {
        await A.CriarRecorrenciaAsync("A");
        var out_ = await A.AbrirAsync(2026, 10);
        await A.DefinirValorAsync(out_.Linhas[0].Id, 700m);
        var nov = await A.AbrirAsync(2026, 11);
        var id = nov.Linhas[0].Id;
        await A.DefinirValorAsync(id, 50m);

        var semSobrescrever = await A.Com<NotaServico, ResultadoLoteDto>(s =>
            s.CopiarValorAnteriorAsync(new CopiarValorAnteriorDto([id], false), default));
        Assert.Equal(1, semSobrescrever.Falhas);
        Assert.Equal(50m, (await A.NotaAsync(id)).Valor);

        var sobrescrevendo = await A.Com<NotaServico, ResultadoLoteDto>(s =>
            s.CopiarValorAnteriorAsync(new CopiarValorAnteriorDto([id], true), default));
        Assert.Equal(1, sobrescrevendo.Sucessos);
        Assert.Equal(700m, (await A.NotaAsync(id)).Valor);
        Assert.Equal(SituacaoNota.Pendente, (await A.NotaAsync(id)).Situacao);
    }

    [Fact]
    public async Task Emissao_exige_emissor_configurado()
    {
        await A.CriarRecorrenciaAsync("A");
        var comp = await A.AbrirAsync();
        await A.DefinirValorAsync(comp.Linhas[0].Id, 10m);
        await A.MarcarProntasAsync(comp.Linhas[0].Id);

        var erro = await Assert.ThrowsAsync<ErroNegocio>(() => A.SolicitarEmissaoAsync(comp.Id!.Value));
        Assert.Contains("emissor", erro.Message);
        Assert.Equal(SituacaoNota.Pronta, (await A.NotaAsync(comp.Linhas[0].Id)).Situacao);
    }
}

public class DocumentoFiscalTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("52998224724", false)]
    [InlineData("111.111.111-11", false)]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000182", false)]
    [InlineData("00000000000000", false)]
    [InlineData("12.ABC.345/01DE-35", true)] // exemplo oficial de CNPJ alfanumérico
    [InlineData("12ABC34501DE34", false)]
    [InlineData("123", false)]
    public void Valida_cpf_e_cnpj(string doc, bool valido) => Assert.Equal(valido, DocumentoFiscal.Valido(doc));

    [Fact]
    public void Formata_documentos()
    {
        Assert.Equal("529.982.247-25", DocumentoFiscal.Formatar("52998224725"));
        Assert.Equal("12.ABC.345/01DE-35", DocumentoFiscal.Formatar("12abc34501de35"));
    }
}
