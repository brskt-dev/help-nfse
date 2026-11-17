using System.Text.Json;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Emissao;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Provedores.Simulacao;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Core.Dados;

/// <summary>
/// Dados FICTÍCIOS de demonstração: emissor, recorrências e três competências anteriores já emitidas em simulação,
/// para testar comparação de valores e o histórico. A competência atual fica por abrir.
/// Só roda em banco sem emissor e sem recorrências.
/// </summary>
public static class SemeadorDemo
{
    private sealed record Cliente(
        string Identificacao, string Documento, string Nome, string Descricao, CamposVariaveis Variaveis,
        decimal?[] Valores, bool Ativa = true, string? Email = null, string? Info = null, bool IssRetido = false,
        ReferenciaServico Referencia = ReferenciaServico.MesAnterior);

    public static async Task<bool> SemearAsync(AppDbContext db, IProvedorNfse provedor, TimeProvider relogio, CancellationToken ct)
    {
        if (await db.Emissores.AnyAsync(ct) || await db.Recorrencias.AnyAsync(ct)) return false;

        var agora = relogio.GetUtcNow();
        var emissor = new Emissor
        {
            RazaoSocial = "Alarmes Exemplo Monitoramento Ltda (FICTÍCIA)",
            NomeFantasia = "Alarmes Exemplo",
            Cnpj = "11222333000181",
            InscricaoMunicipal = null,
            Email = "financeiro@exemplo.invalid",
            Telefone = "19999990000",
            Endereco = new Endereco
            {
                Cep = "13465000", Logradouro = "Rua Exemplo", Numero = "100", Bairro = "Centro",
                CodigoMunicipio = "3501608", NomeMunicipio = "Americana", Uf = "SP",
            },
            OpcaoSimplesNacional = OpcaoSimplesNacional.MeEpp,
            RegimeApuracaoSimples = RegimeApuracaoSimples.TributosFederaisEMunicipalPeloSimples,
            RegimeEspecialTributacao = RegimeEspecialTributacao.Nenhum,
            SerieDps = "1",
            ProximoNumeroDps = 1,
            RegraDataCompetencia = RegraDataCompetencia.DataDaEmissao,
            AtualizadoEm = agora,
        };
        db.Emissores.Add(emissor);

        // Valores das três competências anteriores (mais antiga → mais recente). null = sem linha naquele mês.
        Cliente[] clientes =
        [
            new("Clínica Vida Plena — monitoramento", "12345678000195", "Clínica Vida Plena Ltda (fictícia)",
                "MONITORAMENTO ELETRÔNICO DE ALARME — MÊS DE REFERÊNCIA {MES_REFERENCIA}", CamposVariaveis.Nenhum, [148.20m, 148.20m, 152.68m],
                Email: "financeiro@vidaplena.invalid"),
            new("Escola Aprender Mais — alarme de incêndio", "23456789000195", "Escola Aprender Mais S/A (fictícia)",
                "MONITORAMENTO ELETRÔNICO DE ALARME DE INCÊNDIO — VENCIMENTO DIA 30", CamposVariaveis.Nenhum, [294.46m, 294.46m, 294.46m],
                Referencia: ReferenciaServico.MesmoMes),
            new("Mercado Bom Preço — monitoramento", "34567890000130", "Mercado Bom Preço Ltda (fictícia)",
                "Monitoramento eletrônico de alarme referente a {mes_referencia}", CamposVariaveis.Descricao, [310.00m, 335.50m, 318.90m]),
            new("Ana Souza — residência", "12345678909", "Ana Souza (fictícia)",
                "Monitoramento eletrônico de alarme residencial", CamposVariaveis.Nenhum, [99.90m, 99.90m, 109.90m]),
            new("Condomínio Horizonte — monitoramento (ISS retido)", "45678901000175", "Condomínio Horizonte (fictício)",
                "Monitoramento eletrônico de alarme — referência {referencia}", CamposVariaveis.InformacoesComplementares, [450.00m, 450.00m, 450.00m],
                Info: "Pedido de compra nº (informar a cada mês)", IssRetido: true),
            new("Bruno Lima — escritório", "98765432100", "Bruno Lima (fictício)",
                "Monitoramento eletrônico de alarme", CamposVariaveis.Nenhum, [130.87m, 130.87m, 130.87m]),
            new("Padaria Pão Quente — monitoramento", "12ABC345000188", "Padaria Pão Quente ME (fictícia, CNPJ alfanumérico)",
                "Monitoramento eletrônico de alarme — mês de referência {mes_referencia}", CamposVariaveis.Nenhum, [null, 153.00m, 153.00m]),
            new("Instituto Saber — monitoramento", "56789012000100", "Instituto Saber (fictício)",
                "Monitoramento eletrônico de alarme e CFTV — {mes_ano_referencia}", CamposVariaveis.Descricao, [null, null, 220.00m]),
            new("Loja Antiga — contrato encerrado", "67890123000116", "Loja Antiga Comércio Ltda (fictícia)",
                "Monitoramento eletrônico de alarme — referência {referencia}", CamposVariaveis.Nenhum, [180.00m, 180.00m, null], Ativa: false),
        ];

        var recorrencias = clientes.Select(c => new Recorrencia
        {
            Identificacao = c.Identificacao,
            Ativa = c.Ativa,
            Tomador = new DadosTomador
            {
                Documento = c.Documento, Nome = c.Nome, Email = c.Email,
                Endereco = new Endereco { CodigoMunicipio = "3501608", NomeMunicipio = "Americana", Uf = "SP" },
            },
            Servico = new DadosServico
            {
                Descricao = c.Descricao,
                CodigoTributacaoNacional = "110201",
                CodigoNbs = "118023000",
                CodigoMunicipioPrestacao = "3501608",
                TributacaoIssqn = TributacaoIssqn.OperacaoTributavel,
                IssRetido = c.IssRetido,
                AliquotaIss = c.IssRetido ? 2.00m : null,
                CstIbsCbs = "000",
                ClassificacaoTributariaIbsCbs = "000001",
                IndicadorOperacaoIbsCbs = "030101",
                InformacoesComplementares = c.Info,
            },
            CamposVariaveis = c.Variaveis,
            ReferenciaServico = c.Referencia,
            Observacoes = "Cadastro fictício de demonstração. Códigos fiscais a confirmar com o contador.",
            CriadaEm = agora,
            AtualizadaEm = agora,
            DesativadaEm = c.Ativa ? null : agora,
        }).ToList();
        db.Recorrencias.AddRange(recorrencias);
        await db.SaveChangesAsync(ct);

        var hoje = Servicos.Formatos.ParaBrasilia(agora);
        var inicio = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-3);
        for (var m = 0; m < 3; m++)
        {
            var data = inicio.AddMonths(m);
            var comp = new Competencia { Ano = data.Year, Mes = data.Month, AbertaEm = agora };
            db.Competencias.Add(comp);
            await db.SaveChangesAsync(ct);
            var confirmadaEm = new DateTimeOffset(data.AddMonths(1).AddDays(1).AddHours(13), TimeSpan.Zero);

            for (var i = 0; i < clientes.Length; i++)
            {
                if (clientes[i].Valores[m] is not { } valor) continue;
                var nota = Servicos.CompetenciaServico.CriarLinha(recorrencias[i], comp, agora);
                nota.Valor = valor;
                nota.CamposRevisados = nota.CamposVariaveis;
                if (clientes[i].Info is not null)
                {
                    nota.InformacoesComplementaresModelo = $"Pedido de compra nº {4500 + m}";
                    Servicos.CompetenciaServico.ResolverTextos(nota, comp.Ano, comp.Mes);
                }
                db.Notas.Add(nota);

                // Último mês: uma linha ignorada, para demonstrar a situação.
                if (m == 2 && clientes[i].Identificacao.StartsWith("Bruno"))
                {
                    nota.Situacao = SituacaoNota.Ignorada;
                    nota.MotivoIgnorada = "Aluno em férias neste mês (exemplo).";
                    continue;
                }

                nota.SerieDps = emissor.SerieDps;
                nota.NumeroDps = emissor.ProximoNumeroDps++;
                var dps = ConstrutorDps.Construir(nota, comp, emissor, provedor, nota.SerieDps, nota.NumeroDps.Value, confirmadaEm);
                nota.IdentificadorDps = dps.IdentificadorDps;
                nota.DataCompetencia = dps.DataCompetencia;
                nota.DpsJson = JsonSerializer.Serialize(dps, JsonOpcoes.Padrao);

                var numero = await db.Database.SqlQuery<long>($"SELECT nextval('simulador_numero_nfse') AS \"Value\"").SingleAsync(ct);
                var emitidaEm = confirmadaEm.AddSeconds(5 + i * 3);
                nota.Situacao = SituacaoNota.Emitida;
                nota.NumeroNfse = $"SIM-{numero:D6}";
                nota.ChaveAcesso = $"SIMULACAO-SEM-VALIDADE-{Guid.NewGuid():N}".ToUpperInvariant();
                nota.EmitidaEm = emitidaEm;
                nota.EmissaoSimulada = true;
                nota.UltimaMensagem = "Emitida (SIMULAÇÃO — sem validade fiscal).";
                nota.CenarioSimulacao = CenarioSimulacao.Sucesso;

                var seq = 1;
                void Tentativa(TipoTentativa tipo, ResultadoTentativa res, DateTimeOffset em, string detalhe, List<MensagemProvedor>? msgs = null) =>
                    nota.Tentativas.Add(new TentativaEmissao
                    {
                        Sequencia = seq++, Tipo = tipo, Resultado = res, IniciadaEm = em, FinalizadaEm = em.AddSeconds(1),
                        IdentificadorDps = dps.IdentificadorDps, Provedor = provedor.Nome, WorkerId = "demo", Detalhe = detalhe,
                        Mensagens = msgs ?? [], DadosEnviadosJson = tipo == TipoTentativa.Envio ? nota.DpsJson : null,
                        NumeroNfse = res is ResultadoTentativa.Autorizada or ResultadoTentativa.Encontrada ? nota.NumeroNfse : null,
                        ChaveAcesso = res is ResultadoTentativa.Autorizada or ResultadoTentativa.Encontrada ? nota.ChaveAcesso : null,
                    });

                if (m == 1 && clientes[i].Identificacao.StartsWith("Mercado"))
                {
                    // Exemplo de rejeição corrigida e reenviada.
                    Tentativa(TipoTentativa.Envio, ResultadoTentativa.Rejeitada, emitidaEm.AddMinutes(-30), "DPS rejeitada pelo provedor.",
                        [new MensagemProvedor("SIM-E001", "Rejeição SIMULADA: código de tributação incompatível com o município de prestação.")]);
                    Tentativa(TipoTentativa.Envio, ResultadoTentativa.Autorizada, emitidaEm, "Emissão SIMULADA autorizada (sem validade fiscal).");
                }
                else if (m == 2 && clientes[i].Identificacao.StartsWith("Condomínio"))
                {
                    // Exemplo de resultado incerto reconciliado por consulta, sem reenvio.
                    Tentativa(TipoTentativa.Envio, ResultadoTentativa.Incerta, emitidaEm.AddSeconds(-20), "SIMULAÇÃO: tempo limite excedido após o envio.");
                    Tentativa(TipoTentativa.Consulta, ResultadoTentativa.Encontrada, emitidaEm, "Consulta confirmou a emissão.");
                    nota.UltimaMensagem = "Emissão confirmada por consulta (reconciliação).";
                }
                else
                {
                    Tentativa(TipoTentativa.Envio, ResultadoTentativa.Autorizada, emitidaEm, "Emissão SIMULADA autorizada (sem validade fiscal).");
                }

                db.SimuladorRegistros.Add(new SimuladorRegistro
                {
                    IdentificadorDps = dps.IdentificadorDps, Cenario = CenarioSimulacao.Sucesso, Emitida = true,
                    NumeroNfse = nota.NumeroNfse, ChaveAcesso = nota.ChaveAcesso, EmitidaEm = emitidaEm,
                    ChamadasEnvio = nota.Tentativas.Count(t => t.Tipo == TipoTentativa.Envio), CriadoEm = agora, AtualizadoEm = agora,
                });
            }
            await db.SaveChangesAsync(ct);
        }
        return true;
    }
}
