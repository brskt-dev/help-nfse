using System.Text.Json;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Validacao;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Core.Servicos;

public sealed class NotaServico(AppDbContext db, TimeProvider relogio, IProvedorNfse provedor)
{
    public async Task<NotaDetalheDto> ObterAsync(long id, CancellationToken ct)
    {
        var n = await db.Notas.AsNoTracking().Include(x => x.Competencia).Include(x => x.Tentativas)
                    .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ErroNegocio.NaoEncontrado("Nota");
        var trabalho = await db.Trabalhos.AsNoTracking().Where(t => t.NotaId == id).OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);
        ValorAnteriorDto? anterior = null;
        if (n.RecorrenciaId is { } rid)
        {
            var mapa = await CompetenciaServico.ValoresAnterioresAsync(db, [rid], n.Competencia!.Ano, n.Competencia.Mes, ct);
            anterior = mapa.GetValueOrDefault(rid);
        }

        DadosDps? dps = n.DpsJson is null ? null : JsonSerializer.Deserialize<DadosDps>(n.DpsJson, JsonOpcoes.Padrao);
        return new NotaDetalheDto(
            n.Id, n.CompetenciaId, n.Competencia!.Ano, n.Competencia.Mes, n.Origem, n.RecorrenciaId, n.Identificacao,
            n.Tomador, n.Servico, n.CamposVariaveis, n.CamposPendentesDeRevisao, n.Valor, anterior, n.Situacao,
            n.MotivoIgnorada, n.UltimaMensagem, CompetenciaServico.Pendencias(n), n.CenarioSimulacao, n.SerieDps,
            n.NumeroDps, n.IdentificadorDps, n.DataCompetencia, dps, n.NumeroNfse, n.ChaveAcesso, n.EmitidaEm,
            n.EmissaoSimulada, n.Situacao == SituacaoNota.Emitida, n.XmlAutorizado is not null,
            PodeEditar(n),
            trabalho is null ? null : new TrabalhoDto(trabalho.Id, trabalho.Status, trabalho.ProximaExecucaoEm,
                trabalho.Execucoes, trabalho.FalhasTemporarias, trabalho.ConsultasIndisponiveis, trabalho.UltimaMensagem),
            n.Tentativas.OrderByDescending(t => t.Sequencia).Select(t => new TentativaDto(t.Id, t.Sequencia, t.Tipo,
                t.Resultado, t.IniciadaEm, t.FinalizadaEm, t.IdentificadorDps, t.Provedor, t.WorkerId, t.Detalhe,
                t.Mensagens, t.NumeroNfse, t.ChaveAcesso, t.DadosEnviadosJson is not null)).ToList(),
            n.CriadaEm, n.AtualizadaEm, n.Versao);
    }

    private static bool PodeEditar(Nota n) => n.Situacao is SituacaoNota.Pendente or SituacaoNota.Pronta or SituacaoNota.Erro;

    private static void GarantirEditavel(Nota n)
    {
        var msg = n.Situacao switch
        {
            SituacaoNota.Emitida => "Nota emitida não pode ser editada.",
            SituacaoNota.Processando => "A nota está em processamento e não pode ser editada.",
            SituacaoNota.ResultadoAConfirmar => "O resultado da emissão ainda está sendo confirmado. A nota não pode ser editada.",
            SituacaoNota.Ignorada => "Restaure a nota antes de editá-la.",
            _ => null,
        };
        if (msg is not null) throw ErroNegocio.Conflito(msg);
    }

    /// <summary>Qualquer edição invalida a revisão: linhas prontas ou com erro voltam para pendente.</summary>
    private void MarcarEditada(Nota n)
    {
        if (n.Situacao is SituacaoNota.Pronta or SituacaoNota.Erro) n.Situacao = SituacaoNota.Pendente;
        n.AtualizadaEm = relogio.GetUtcNow();
    }

    public async Task<NotaDetalheDto> AtualizarAsync(long id, AtualizarNotaDto dto, CancellationToken ct)
    {
        var n = await db.Notas.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ErroNegocio.NaoEncontrado("Nota");
        GarantirEditavel(n);
        db.Entry(n).Property(x => x.Versao).OriginalValue = dto.Versao;

        var erros = new List<string>();
        if (dto.Valor is { } v) Validadores.ValidarValor(v, erros);
        n.Valor = dto.Valor;

        if (n.Origem == OrigemNota.Avulsa)
        {
            if (dto.Identificacao is not null) n.Identificacao = dto.Identificacao.Trim();
            if (dto.Tomador is not null) n.Tomador = Validadores.NormalizarTomador(dto.Tomador);
            if (dto.Servico is not null) n.Servico = Validadores.NormalizarServico(dto.Servico);
            if (string.IsNullOrWhiteSpace(n.Identificacao)) erros.Add("Informe a identificação.");
            Validadores.ValidarTomador(n.Tomador, erros);
            Validadores.ValidarServico(n.Servico, erros, exigirCodigos: false);
        }
        else
        {
            // Linhas de recorrência: somente os campos marcados como variáveis são editáveis.
            if (n.CamposVariaveis.HasFlag(CamposVariaveis.Descricao) && dto.Descricao is not null)
            {
                n.Servico = n.Servico with { Descricao = dto.Descricao.Trim() };
                n.CamposRevisados |= CamposVariaveis.Descricao;
            }
            if (n.CamposVariaveis.HasFlag(CamposVariaveis.InformacoesComplementares) && dto.InformacoesComplementares is not null)
            {
                n.Servico = n.Servico with { InformacoesComplementares = Formatos.Limpar(dto.InformacoesComplementares) };
                n.CamposRevisados |= CamposVariaveis.InformacoesComplementares;
            }
            Validadores.ValidarServico(n.Servico, erros, exigirCodigos: false);
        }

        if (provedor.Simulado && dto.CenarioSimulacao is { } c) n.CenarioSimulacao = c;
        if (erros.Count > 0) throw new ErroNegocio("Verifique os dados da nota.", 400, erros);

        MarcarEditada(n);
        await SalvarAsync(ct);
        return await ObterAsync(id, ct);
    }

    public async Task<LinhaResumoDto> AtualizarValorAsync(long id, AtualizarValorDto dto, CancellationToken ct)
    {
        var n = await db.Notas.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ErroNegocio.NaoEncontrado("Nota");
        GarantirEditavel(n);
        db.Entry(n).Property(x => x.Versao).OriginalValue = dto.Versao;
        if (dto.Valor is { } v)
        {
            var erros = new List<string>();
            Validadores.ValidarValor(v, erros);
            if (erros.Count > 0) throw new ErroNegocio(erros[0], 400, erros);
        }
        var voltou = n.Situacao is SituacaoNota.Pronta or SituacaoNota.Erro && n.Valor != dto.Valor;
        if (n.Valor == dto.Valor) return new LinhaResumoDto(n.Id, n.Situacao, n.Valor, n.Versao, false);
        n.Valor = dto.Valor;
        MarcarEditada(n);
        await SalvarAsync(ct);
        return new LinhaResumoDto(n.Id, n.Situacao, n.Valor, n.Versao, voltou);
    }

    public async Task<ResultadoLoteDto> CopiarValorAnteriorAsync(CopiarValorAnteriorDto dto, CancellationToken ct)
    {
        var notas = await CarregarAsync(dto.Ids, ct);
        var anteriores = new Dictionary<long, ValorAnteriorDto>();
        foreach (var grupo in notas.Where(n => n.RecorrenciaId != null).GroupBy(n => (n.Competencia!.Ano, n.Competencia.Mes)))
        {
            var mapa = await CompetenciaServico.ValoresAnterioresAsync(db,
                grupo.Select(n => n.RecorrenciaId!.Value).ToList(), grupo.Key.Ano, grupo.Key.Mes, ct);
            foreach (var n in grupo)
                if (mapa.TryGetValue(n.RecorrenciaId!.Value, out var va)) anteriores[n.Id] = va;
        }

        return await EmLoteAsync(dto.Ids, notas, n =>
        {
            if (!PodeEditar(n)) return [MensagemSituacao(n)];
            if (!anteriores.TryGetValue(n.Id, out var va)) return ["Não há valor em competência anterior."];
            if (n.Valor == va.Valor) return [];
            if (n.Valor is not null && !dto.Sobrescrever) return ["A linha já possui valor; não foi sobrescrito."];
            n.Valor = va.Valor;
            MarcarEditada(n);
            return [];
        }, ct);
    }

    public async Task<ResultadoLoteDto> MarcarProntasAsync(List<long> ids, CancellationToken ct)
    {
        var notas = await CarregarAsync(ids, ct);
        return await EmLoteAsync(ids, notas, n =>
        {
            if (n.Situacao == SituacaoNota.Pronta) return [];
            if (n.Situacao is not (SituacaoNota.Pendente or SituacaoNota.Erro)) return [MensagemSituacao(n)];
            var erros = Validadores.ValidarNotaParaPronta(n);
            if (erros.Count > 0) return erros;
            n.Situacao = SituacaoNota.Pronta;
            n.AtualizadaEm = relogio.GetUtcNow();
            return [];
        }, ct);
    }

    public async Task<ResultadoLoteDto> VoltarParaPendenteAsync(List<long> ids, CancellationToken ct)
    {
        var notas = await CarregarAsync(ids, ct);
        return await EmLoteAsync(ids, notas, n =>
        {
            if (n.Situacao == SituacaoNota.Pendente) return [];
            if (n.Situacao != SituacaoNota.Pronta) return [MensagemSituacao(n)];
            n.Situacao = SituacaoNota.Pendente;
            n.AtualizadaEm = relogio.GetUtcNow();
            return [];
        }, ct);
    }

    public async Task<ResultadoLoteDto> IgnorarAsync(IgnorarDto dto, CancellationToken ct)
    {
        var notas = await CarregarAsync(dto.Ids, ct);
        return await EmLoteAsync(dto.Ids, notas, n =>
        {
            if (n.Situacao == SituacaoNota.Ignorada) return [];
            if (!PodeEditar(n)) return [MensagemSituacao(n)];
            n.Situacao = SituacaoNota.Ignorada;
            n.MotivoIgnorada = Formatos.Limpar(dto.Motivo);
            n.AtualizadaEm = relogio.GetUtcNow();
            return [];
        }, ct);
    }

    public async Task<ResultadoLoteDto> RestaurarAsync(List<long> ids, CancellationToken ct)
    {
        var notas = await CarregarAsync(ids, ct);
        return await EmLoteAsync(ids, notas, n =>
        {
            if (n.Situacao != SituacaoNota.Ignorada) return ["A nota não está ignorada."];
            n.Situacao = SituacaoNota.Pendente;
            n.MotivoIgnorada = null;
            n.AtualizadaEm = relogio.GetUtcNow();
            return [];
        }, ct);
    }

    public async Task<NotaDetalheDto> CriarAvulsaAsync(long competenciaId, CriarAvulsaDto dto, CancellationToken ct)
    {
        var comp = await db.Competencias.SingleOrDefaultAsync(c => c.Id == competenciaId, ct)
                   ?? throw ErroNegocio.NaoEncontrado("Competência");
        var agora = relogio.GetUtcNow();
        var n = new Nota
        {
            CompetenciaId = comp.Id,
            Origem = OrigemNota.Avulsa,
            Identificacao = (dto.Identificacao ?? "").Trim(),
            Tomador = Validadores.NormalizarTomador(dto.Tomador ?? new()),
            Servico = Validadores.NormalizarServico(dto.Servico ?? new()),
            Valor = dto.Valor,
            Situacao = SituacaoNota.Pendente,
            CenarioSimulacao = provedor.Simulado ? dto.CenarioSimulacao ?? CenarioSimulacao.Sucesso : CenarioSimulacao.Sucesso,
            CriadaEm = agora,
            AtualizadaEm = agora,
        };
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(n.Identificacao)) erros.Add("Informe a identificação.");
        if (n.Valor is { } v) Validadores.ValidarValor(v, erros);
        Validadores.ValidarTomador(n.Tomador, erros);
        Validadores.ValidarServico(n.Servico, erros, exigirCodigos: false);
        if (erros.Count > 0) throw new ErroNegocio("Verifique os dados da nota avulsa.", 400, erros);

        db.Notas.Add(n);
        await db.SaveChangesAsync(ct);
        return await ObterAsync(n.Id, ct);
    }

    public async Task ExcluirAvulsaAsync(long id, CancellationToken ct)
    {
        var n = await db.Notas.Include(x => x.Tentativas).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ErroNegocio.NaoEncontrado("Nota");
        if (n.Origem != OrigemNota.Avulsa) throw ErroNegocio.Conflito("Somente notas avulsas podem ser excluídas. Use \"Ignorar neste mês\".");
        if (n.Tentativas.Count > 0 || n.NumeroDps is not null || !PodeEditar(n))
            throw ErroNegocio.Conflito("Esta nota já teve tentativas de emissão e faz parte do histórico. Use \"Ignorar neste mês\".");
        db.Notas.Remove(n);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Atualiza explicitamente a cópia dos dados da linha a partir do cadastro atual da recorrência.</summary>
    public async Task<NotaDetalheDto> AtualizarDoCadastroAsync(long id, CancellationToken ct)
    {
        var n = await db.Notas.Include(x => x.Competencia).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ErroNegocio.NaoEncontrado("Nota");
        GarantirEditavel(n);
        if (n.RecorrenciaId is null) throw ErroNegocio.Conflito("Nota avulsa não possui cadastro de recorrência.");
        var r = await db.Recorrencias.SingleAsync(x => x.Id == n.RecorrenciaId, ct);
        var nova = CompetenciaServico.CriarLinha(r, n.Competencia!, relogio.GetUtcNow());
        n.Identificacao = nova.Identificacao;
        n.Tomador = nova.Tomador;
        n.Servico = nova.Servico;
        n.CamposVariaveis = nova.CamposVariaveis;
        n.CamposRevisados = CamposVariaveis.Nenhum;
        MarcarEditada(n);
        await SalvarAsync(ct);
        return await ObterAsync(id, ct);
    }

    /// <summary>Pede ao worker uma nova consulta de reconciliação para uma nota com resultado a confirmar.</summary>
    public async Task SolicitarConsultaAsync(long id, CancellationToken ct)
    {
        var n = await db.Notas.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ErroNegocio.NaoEncontrado("Nota");
        if (n.Situacao != SituacaoNota.ResultadoAConfirmar)
            throw ErroNegocio.Conflito("A consulta só se aplica a notas com resultado a confirmar.");
        var agora = relogio.GetUtcNow();
        var t = await db.Trabalhos.Where(x => x.NotaId == id && (x.Status == StatusTrabalho.Pendente
                || x.Status == StatusTrabalho.Suspenso || x.Status == StatusTrabalho.EmExecucao))
            .SingleOrDefaultAsync(ct);
        if (t is null)
        {
            db.Trabalhos.Add(new TrabalhoEmissao
            {
                NotaId = id, Status = StatusTrabalho.Pendente, ProximaExecucaoEm = agora, CriadoEm = agora, AtualizadoEm = agora,
                UltimaMensagem = "Consulta solicitada pelo usuário.",
            });
        }
        else if (t.Status != StatusTrabalho.EmExecucao)
        {
            t.Status = StatusTrabalho.Pendente;
            t.ProximaExecucaoEm = agora;
            t.ConsultasIndisponiveis = 0;
            t.UltimaMensagem = "Consulta solicitada pelo usuário.";
            t.AtualizadoEm = agora;
        }
        await db.SaveChangesAsync(ct);
    }

    private static string MensagemSituacao(Nota n) => n.Situacao switch
    {
        SituacaoNota.Emitida => "Nota já emitida.",
        SituacaoNota.Processando => "Nota em processamento.",
        SituacaoNota.ResultadoAConfirmar => "Resultado da emissão a confirmar.",
        SituacaoNota.Ignorada => "Nota ignorada neste mês.",
        SituacaoNota.Pronta => "Nota já está pronta.",
        _ => $"Situação {n.Situacao} não permite esta ação.",
    };

    private Task<List<Nota>> CarregarAsync(List<long> ids, CancellationToken ct) =>
        db.Notas.Include(n => n.Competencia).Where(n => ids.Contains(n.Id)).ToListAsync(ct);

    private async Task<ResultadoLoteDto> EmLoteAsync(List<long> ids, List<Nota> notas, Func<Nota, List<string>> acao, CancellationToken ct)
    {
        var itens = new List<ResultadoItemDto>();
        foreach (var id in ids.Distinct())
        {
            var n = notas.FirstOrDefault(x => x.Id == id);
            if (n is null)
            {
                itens.Add(new ResultadoItemDto(id, false, "", "Nota não encontrada.", []));
                continue;
            }
            var erros = acao(n);
            itens.Add(new ResultadoItemDto(id, erros.Count == 0, n.Identificacao, erros.FirstOrDefault(), erros));
        }
        await SalvarAsync(ct);
        return new ResultadoLoteDto(itens.Count(i => i.Sucesso), itens.Count(i => !i.Sucesso), itens);
    }

    private async Task SalvarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ErroNegocio.Conflito("A nota foi alterada por outra operação. Recarregue a competência e tente novamente.");
        }
    }
}

public sealed record LinhaResumoDto(long Id, SituacaoNota Situacao, decimal? Valor, uint Versao, bool VoltouParaPendente);
