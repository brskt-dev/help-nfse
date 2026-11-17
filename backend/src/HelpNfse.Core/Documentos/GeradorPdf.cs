using System.Text.Json;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Servicos;
using HelpNfse.Core.Validacao;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HelpNfse.Core.Documentos;

/// <summary>
/// Gera, sob demanda, o PDF de uma nota emitida em SIMULAÇÃO. Usa somente os dados congelados da própria nota
/// (DPS e resultado), nunca o cadastro atual. Não é um DANFSe e não tem validade fiscal.
/// O DANFSe real será tratado na etapa de integração, conforme o leiaute oficial.
/// </summary>
public static class GeradorPdf
{
    public const string AvisoSimulacao = "SIMULAÇÃO — SEM VALIDADE FISCAL";

    static GeradorPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] GerarSimulacao(Nota nota, IReadOnlyList<TentativaEmissao> tentativas, DateTimeOffset geradoEm)
    {
        if (nota.Situacao != SituacaoNota.Emitida || nota.DpsJson is null)
            throw new ErroNegocio("O documento só está disponível para notas emitidas.", 404);
        if (!nota.EmissaoSimulada)
            throw new ErroNegocio("Documento de nota real deve ser o DANFSe oficial (não disponível nesta versão).", 404);

        var dps = JsonSerializer.Deserialize<DadosDps>(nota.DpsJson, JsonOpcoes.Padrao)!;
        var vermelho = Color.FromHex("#B3261E");

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(x => x.FontSize(9.5f));

            page.Background().AlignCenter().AlignMiddle().Rotate(-35)
                .Text("SIMULAÇÃO\nSEM VALIDADE FISCAL").FontSize(56).Bold().FontColor(Color.FromHex("#F3D6D4"))
                .AlignCenter();

            page.Header().Column(c =>
            {
                c.Item().Background(vermelho).Padding(6).AlignCenter()
                    .Text(AvisoSimulacao).FontColor(Colors.White).Bold().FontSize(13);
                c.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Column(cc =>
                    {
                        cc.Item().Text("Documento de simulação de emissão").Bold().FontSize(14);
                        cc.Item().Text("Este documento NÃO é uma NFS-e nem um DANFSe. Foi gerado pelo help-nfse em modo de simulação, "
                                       + "apenas para validar o fluxo de trabalho. Nenhuma nota fiscal foi emitida.")
                            .FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                    });
                    r.ConstantItem(170).AlignRight().Column(cc =>
                    {
                        cc.Item().AlignRight().Text($"Número simulado: {nota.NumeroNfse}").Bold();
                        cc.Item().AlignRight().Text($"Mês de emissão: {dps.CompetenciaMes:00}/{dps.CompetenciaAno}");
                        if (dps.MesReferenciaServico is { } mr)
                            cc.Item().AlignRight().Text($"Referência do serviço: {mr.Month:00}/{mr.Year}");
                        cc.Item().AlignRight().Text($"Emitida (simulação): {Formatos.DataHora(nota.EmitidaEm!.Value)}");
                    });
                });
                c.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });

            page.Content().PaddingVertical(8).Column(c =>
            {
                c.Spacing(8);
                Secao(c, "Identificação (simulada)", t =>
                {
                    Linha(t, "Chave simulada", nota.ChaveAcesso);
                    Linha(t, "Identificador da DPS", dps.IdentificadorDps);
                    Linha(t, "Série / número da DPS", $"{dps.Serie} / {dps.Numero}");
                    Linha(t, "Data de competência (DPS)", dps.DataCompetencia.ToString("dd/MM/yyyy"));
                    Linha(t, "Provedor", "Simulador do help-nfse (sem comunicação com a NFS-e Nacional)");
                });
                Secao(c, "Prestador", t =>
                {
                    Linha(t, "Razão social", dps.Prestador.RazaoSocial);
                    Linha(t, "CNPJ", DocumentoFiscal.Formatar(dps.Prestador.Cnpj));
                    Linha(t, "Inscrição municipal", dps.Prestador.InscricaoMunicipal);
                    Linha(t, "Endereço", Endereco(dps.Prestador.Endereco));
                    Linha(t, "Simples Nacional", Rotulos.De(dps.Prestador.OpcaoSimplesNacional));
                    Linha(t, "Regime especial", Rotulos.De(dps.Prestador.RegimeEspecialTributacao));
                });
                Secao(c, "Tomador", t =>
                {
                    Linha(t, "Nome / razão social", dps.Tomador.Nome);
                    Linha(t, "CPF/CNPJ", DocumentoFiscal.Formatar(dps.Tomador.Documento));
                    Linha(t, "E-mail", dps.Tomador.Email);
                    Linha(t, "Endereço", Endereco(dps.Tomador.Endereco));
                });
                Secao(c, "Serviço", t =>
                {
                    Linha(t, "Descrição", dps.Servico.Descricao);
                    Linha(t, "Cód. tributação nacional", dps.Servico.CodigoTributacaoNacional);
                    Linha(t, "Cód. tributação municipal", dps.Servico.CodigoTributacaoMunicipal);
                    Linha(t, "Código NBS", dps.Servico.CodigoNbs);
                    Linha(t, "Município de prestação (IBGE)", dps.Servico.CodigoMunicipioPrestacao);
                    Linha(t, "Tributação do ISSQN", Rotulos.De(dps.Servico.TributacaoIssqn));
                    Linha(t, "Alíquota ISS informada", dps.Servico.AliquotaIss is { } a ? $"{a:0.##}%" : null);
                    Linha(t, "ISS retido", dps.Servico.IssRetido ? "Sim" : "Não");
                    Linha(t, "IBS/CBS — CST / cClassTrib / ind. operação",
                        $"{dps.Servico.CstIbsCbs ?? "—"} / {dps.Servico.ClassificacaoTributariaIbsCbs ?? "—"} / {dps.Servico.IndicadorOperacaoIbsCbs ?? "—"}");
                    Linha(t, "Informações complementares", dps.Servico.InformacoesComplementares);
                });
                c.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                {
                    r.RelativeItem().Text("Valor dos serviços").Bold().FontSize(12);
                    r.AutoItem().Text(Formatos.Moeda(dps.Valor)).Bold().FontSize(12);
                });
                c.Item().Text("Tributos não são calculados pelo help-nfse. Valores fiscais definitivos dependem da integração oficial.")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);

                Secao(c, "Histórico de tentativas", t =>
                {
                    foreach (var tt in tentativas.OrderBy(x => x.Sequencia))
                        Linha(t, $"#{tt.Sequencia} {Rotulos.De(tt.Tipo)} — {Formatos.DataHora(tt.IniciadaEm)}", $"{Rotulos.De(tt.Resultado)}. {tt.Detalhe}");
                });
            });

            page.Footer().Column(c =>
            {
                c.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                c.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text(AvisoSimulacao).Bold().FontColor(vermelho);
                    r.AutoItem().Text(t =>
                    {
                        t.Span($"Gerado em {Formatos.DataHora(geradoEm)} · página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            });
        })).GeneratePdf();
    }

    private static void Secao(ColumnDescriptor c, string titulo, Action<TableDescriptor> conteudo)
    {
        c.Item().Column(cc =>
        {
            cc.Item().Text(titulo).Bold().FontSize(11);
            cc.Item().PaddingTop(2).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(165);
                    cd.RelativeColumn();
                });
                conteudo(t);
            });
        });
    }

    private static void Linha(TableDescriptor t, string rotulo, string? valor)
    {
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).Text(rotulo).FontColor(Colors.Grey.Darken2);
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor);
    }

    private static string Endereco(Endereco e)
    {
        var partes = new[]
        {
            string.Join(", ", new[] { e.Logradouro, e.Numero, e.Complemento }.Where(x => !string.IsNullOrWhiteSpace(x))),
            e.Bairro,
            string.Join("/", new[] { e.NomeMunicipio, e.Uf }.Where(x => !string.IsNullOrWhiteSpace(x))),
            e.Cep is { Length: 8 } cep ? $"CEP {cep[..5]}-{cep[5..]}" : e.Cep,
        };
        return string.Join(" · ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
