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
        decimal?[] Valores, bool Ativa = true, string? Email = null, string? Info = null);

    public static async Task<bool> SemearAsync(AppDbContext db, IProvedorNfse provedor, TimeProvider relogio, CancellationToken ct)
    {
        if (await db.Emissores.AnyAsync(ct) || await db.Recorrencias.AnyAsync(ct)) return false;

        var agora = relogio.GetUtcNow();
        var emissor = new Emissor
        {
            RazaoSocial = "Prestadora Exemplo Serviços Ltda (FICTÍCIA)",
            NomeFantasia = "Exemplo Serviços",
            Cnpj = "11222333000181",
            InscricaoMunicipal = "1234567",
            Email = "contato@exemplo.invalid",
            Telefone = "11999990000",
            Endereco = new Endereco
            {
                Cep = "01001000", Logradouro = "Praça da Sé", Numero = "100", Complemento = "Sala 1", Bairro = "Sé",
                CodigoMunicipio = "3550308", NomeMunicipio = "São Paulo", Uf = "SP",
            },
            OpcaoSimplesNacional = OpcaoSimplesNacional.MeEpp,
            RegimeApuracaoSimples = RegimeApuracaoSimples.TributosFederaisEMunicipalPeloSimples,
            RegimeEspecialTributacao = RegimeEspecialTributacao.Nenhum,
            SerieDps = "1",
            ProximoNumeroDps = 1,
            RegraDataCompetencia = RegraDataCompetencia.UltimoDiaDoMes,
            AtualizadoEm = agora,
        };
        db.Emissores.Add(emissor);

        // Valores das três competências anteriores (mais antiga → mais recente). null = sem linha naquele mês.
        Cliente[] clientes =
        [
            new("Clínica Vida Plena — consultoria", "12345678000195", "Clínica Vida Plena Ltda (fictícia)",
                "Consultoria em gestão administrativa — competência {competencia}", CamposVariaveis.Nenhum, [2500m, 2500m, 2650m],
                Email: "financeiro@vidaplena.invalid"),
            new("Escola Aprender Mais — suporte de TI", "23456789000195", "Escola Aprender Mais S/A (fictícia)",
                "Suporte técnico em informática — {mes_ano}", CamposVariaveis.Nenhum, [1800m, 1800m, 1800m]),
            new("Mercado Bom Preço — rotinas administrativas", "34567890000130", "Mercado Bom Preço Ltda (fictícia)",
                "Serviços administrativos referentes a {mes_ano}", CamposVariaveis.Descricao, [3200m, 3350m, 3100m]),
            new("Ana Souza — mentoria", "12345678909", "Ana Souza (fictícia)",
                "Mentoria profissional — {mes_ano}", CamposVariaveis.Nenhum, [600m, 600m, 750m]),
            new("Construtora Horizonte — manutenção de sistemas", "45678901000175", "Construtora Horizonte Ltda (fictícia)",
                "Manutenção mensal de sistemas — {competencia}", CamposVariaveis.InformacoesComplementares, [4500m, 4500m, 4500m],
                Info: "Pedido de compra nº (informar a cada mês)"),
            new("Bruno Lima — aulas particulares", "98765432100", "Bruno Lima (fictício)",
                "Aulas particulares de matemática — {mes_ano}", CamposVariaveis.Nenhum, [350m, 400m, 400m]),
            new("Padaria Pão Quente — site", "12ABC345000188", "Padaria Pão Quente ME (fictícia, CNPJ alfanumérico)",
                "Hospedagem e manutenção de site — {competencia}", CamposVariaveis.Nenhum, [null, 250m, 250m]),
            new("Instituto Saber — palestras", "56789012000100", "Instituto Saber (fictício)",
                "Palestras e treinamentos — {mes_ano}", CamposVariaveis.Descricao, [null, null, 1200m]),
            new("Loja Antiga — encerrado", "67890123000116", "Loja Antiga Comércio Ltda (fictícia)",
                "Consultoria comercial — {competencia}", CamposVariaveis.Nenhum, [900m, 900m, null], Ativa: false),
        ];

        var recorrencias = clientes.Select(c => new Recorrencia
        {
            Identificacao = c.Identificacao,
            Ativa = c.Ativa,
            Tomador = new DadosTomador
            {
                Documento = c.Documento, Nome = c.Nome, Email = c.Email,
                Endereco = new Endereco { CodigoMunicipio = "3550308", NomeMunicipio = "São Paulo", Uf = "SP" },
            },
            Servico = new DadosServico
            {
                Descricao = c.Descricao,
                CodigoTributacaoNacional = "010101",
                CodigoMunicipioPrestacao = "3550308",
                TributacaoIssqn = TributacaoIssqn.OperacaoTributavel,
                AliquotaIss = 2m,
                InformacoesComplementares = c.Info,
            },
            CamposVariaveis = c.Variaveis,
            Observacoes = "Cadastro fictício de demonstração.",
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
                    nota.Servico = nota.Servico with { InformacoesComplementares = $"Pedido de compra nº {4500 + m}" };
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
                else if (m == 2 && clientes[i].Identificacao.StartsWith("Construtora"))
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
