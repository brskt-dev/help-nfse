using System.Text.Json;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Servicos;
using HelpNfse.Core.Validacao;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HelpNfse.Core.Emissao;

/// <summary>
/// Registra pedidos de emissão. Não chama o provedor: apenas move as notas prontas para "Processando"
/// e cria os trabalhos na fila, que o worker processa de forma independente do navegador.
/// </summary>
public sealed class EmissaoServico(AppDbContext db, TimeProvider relogio, IProvedorNfse provedor)
{
    public async Task<PreviaEmissaoDto> PreviaAsync(long competenciaId, CancellationToken ct)
    {
        _ = await db.Competencias.AsNoTracking().SingleOrDefaultAsync(c => c.Id == competenciaId, ct)
            ?? throw ErroNegocio.NaoEncontrado("Competência");
        var prontas = await db.Notas.AsNoTracking()
            .Where(n => n.CompetenciaId == competenciaId && n.Situacao == SituacaoNota.Pronta)
            .OrderBy(n => n.Tomador.Nome).ThenBy(n => n.Id).ToListAsync(ct);
        var emissor = await db.Emissores.AsNoTracking().SingleOrDefaultAsync(ct);
        var bloqueios = Validadores.ValidarEmissorParaEmissao(emissor);
        return new PreviaEmissaoDto(prontas.Count, prontas.Sum(n => n.Valor ?? 0),
            prontas.Select(n => new ItemPreviaDto(n.Id, n.Identificacao, n.Tomador.Nome, n.Tomador.Documento, n.Valor ?? 0)).ToList(),
            bloqueios, provedor.Nome, provedor.Simulado);
    }

    public async Task<LoteDto> SolicitarAsync(long competenciaId, SolicitarEmissaoDto dto, string? usuario, CancellationToken ct)
    {
        if (dto.ChaveIdempotencia == Guid.Empty) throw new ErroNegocio("Chave de idempotência ausente.");

        // Clique repetido com a mesma confirmação: devolve o lote já registrado.
        if (await BuscarLoteAsync(dto.ChaveIdempotencia, ct) is { } existente) return existente;
        if (dto.NotaIds is not { Count: > 0 }) throw new ErroNegocio("Nenhuma nota selecionada para emissão.");

        var agora = relogio.GetUtcNow();
        var ids = dto.NotaIds.Distinct().ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Bloqueia emissor (numeração da DPS) e as notas envolvidas até o fim da transação.
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM emissor WHERE id = 1 FOR UPDATE", ct);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM notas WHERE id = ANY({ids.ToArray()}) ORDER BY id FOR UPDATE", ct);

        // Após obter os bloqueios, outro clique com a mesma chave pode ter concluído antes.
        if (await BuscarLoteAsync(dto.ChaveIdempotencia, ct) is { } concorrente) return concorrente;

        var emissor = await db.Emissores.SingleOrDefaultAsync(ct);
        var bloqueios = Validadores.ValidarEmissorParaEmissao(emissor);
        if (bloqueios.Count > 0) throw new ErroNegocio("Não é possível emitir: revise a configuração do emissor.", 400, bloqueios);

        var comp = await db.Competencias.SingleOrDefaultAsync(c => c.Id == competenciaId, ct)
                   ?? throw ErroNegocio.NaoEncontrado("Competência");
        var notas = await db.Notas.Where(n => ids.Contains(n.Id)).ToListAsync(ct);

        var foraDoEsperado = notas.Count != ids.Count
                             || notas.Any(n => n.CompetenciaId != competenciaId || n.Situacao != SituacaoNota.Pronta);
        var total = notas.Sum(n => n.Valor ?? 0);
        if (foraDoEsperado || total != dto.ValorTotal)
            throw ErroNegocio.Conflito("As notas prontas mudaram desde a confirmação. Revise a lista e confirme novamente.");

        var invalidas = notas.Select(n => (n, erros: Validadores.ValidarNotaParaPronta(n))).Where(x => x.erros.Count > 0).ToList();
        if (invalidas.Count > 0)
            throw new ErroNegocio("Algumas notas deixaram de ser válidas. Revise antes de emitir.", 400,
                invalidas.Select(x => $"{x.n.Identificacao}: {x.erros[0]}").ToList());

        var lote = new LoteEmissao
        {
            CompetenciaId = competenciaId,
            ChaveIdempotencia = dto.ChaveIdempotencia,
            Quantidade = notas.Count,
            ValorTotal = total,
            SolicitadoPor = usuario,
            CriadoEm = agora,
        };
        db.Lotes.Add(lote);
        await db.SaveChangesAsync(ct);

        foreach (var n in notas.OrderBy(n => n.Tomador.Nome).ThenBy(n => n.Id))
        {
            // O número da DPS é atribuído uma única vez por nota e reutilizado em reenvios,
            // para que o serviço remoto reconheça duplicidades e a consulta reconcilie resultados incertos.
            if (n.NumeroDps is null)
            {
                n.SerieDps = emissor!.SerieDps;
                n.NumeroDps = emissor.ProximoNumeroDps++;
            }
            var dps = ConstrutorDps.Construir(n, comp, emissor!, provedor, n.SerieDps!, n.NumeroDps.Value, agora);
            n.IdentificadorDps = dps.IdentificadorDps;
            n.DataCompetencia = dps.DataCompetencia;
            n.DpsJson = JsonSerializer.Serialize(dps, JsonOpcoes.Padrao);
            n.Situacao = SituacaoNota.Processando;
            n.UltimaMensagem = "Aguardando processamento.";
            n.AtualizadaEm = agora;

            db.Trabalhos.Add(new TrabalhoEmissao
            {
                NotaId = n.Id,
                LoteId = lote.Id,
                Status = StatusTrabalho.Pendente,
                ProximaExecucaoEm = agora,
                CriadoEm = agora,
                AtualizadoEm = agora,
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ErroNegocio.Conflito("Já existe um processamento em andamento para alguma das notas.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ErroNegocio.Conflito("As notas foram alteradas durante a confirmação. Revise e confirme novamente.");
        }
        await tx.CommitAsync(ct);
        return new LoteDto(lote.Id, lote.ChaveIdempotencia, lote.Quantidade, lote.ValorTotal, lote.CriadoEm, false);
    }

    private async Task<LoteDto?> BuscarLoteAsync(Guid chave, CancellationToken ct)
    {
        var l = await db.Lotes.AsNoTracking().SingleOrDefaultAsync(x => x.ChaveIdempotencia == chave, ct);
        return l is null ? null : new LoteDto(l.Id, l.ChaveIdempotencia, l.Quantidade, l.ValorTotal, l.CriadoEm, true);
    }
}
