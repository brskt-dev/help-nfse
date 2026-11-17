using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Validacao;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Core.Servicos;

public sealed class CompetenciaServico(AppDbContext db, TimeProvider relogio)
{
    public static void ValidarMes(int ano, int mes)
    {
        if (ano is < 2000 or > 2100 || mes is < 1 or > 12) throw new ErroNegocio("Competência inválida.");
    }

    public async Task<List<CompetenciaResumoDto>> ListarAsync(CancellationToken ct)
    {
        var comps = await db.Competencias.AsNoTracking().OrderByDescending(c => c.Ano).ThenByDescending(c => c.Mes).ToListAsync(ct);
        var agregados = await db.Notas.AsNoTracking()
            .GroupBy(n => new { n.CompetenciaId, n.Situacao })
            .Select(g => new { g.Key.CompetenciaId, g.Key.Situacao, Qtd = g.Count(), Total = g.Sum(n => n.Valor ?? 0) })
            .ToListAsync(ct);
        return comps.Select(c =>
        {
            var resumo = agregados.Where(a => a.CompetenciaId == c.Id)
                .Select(a => new ResumoSituacaoDto(a.Situacao, a.Qtd, a.Total)).OrderBy(r => r.Situacao).ToList();
            return new CompetenciaResumoDto(c.Id, c.Ano, c.Mes, c.AbertaEm, resumo, resumo.Sum(r => r.Quantidade),
                resumo.Where(r => r.Situacao == SituacaoNota.Emitida).Sum(r => r.Total));
        }).ToList();
    }

    /// <summary>
    /// Abre a competência e gera as linhas das recorrências ativas que ainda não têm linha nela.
    /// Idempotente: repetir a operação não duplica linhas (lock por competência + índice único).
    /// Linhas novas nascem pendentes, sem valor: nada é copiado silenciosamente.
    /// </summary>
    public async Task<CompetenciaDto> AbrirAsync(int ano, int mes, CancellationToken ct)
    {
        ValidarMes(ano, mes);
        var agora = relogio.GetUtcNow();

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            // Serializa aberturas concorrentes da mesma competência.
            var chaveLock = 7_000_000_000L + Competencia.ChaveDe(ano, mes);
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({chaveLock})", ct);

            var comp = await db.Competencias.SingleOrDefaultAsync(c => c.Ano == ano && c.Mes == mes, ct);
            if (comp is null)
            {
                comp = new Competencia { Ano = ano, Mes = mes, AbertaEm = agora };
                db.Competencias.Add(comp);
                await db.SaveChangesAsync(ct);
            }

            var existentes = await db.Notas.Where(n => n.CompetenciaId == comp.Id && n.RecorrenciaId != null)
                .Select(n => n.RecorrenciaId!.Value).ToListAsync(ct);
            var novas = await db.Recorrencias.Where(r => r.Ativa && !existentes.Contains(r.Id)).ToListAsync(ct);

            foreach (var r in novas) db.Notas.Add(CriarLinha(r, comp, agora));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        db.ChangeTracker.Clear();
        return await ObterAsync(ano, mes, ct);
    }

    internal static Nota CriarLinha(Recorrencia r, Competencia comp, DateTimeOffset agora)
    {
        var servico = r.Servico.Copiar();
        servico.Descricao = ModeloDescricao.Aplicar(servico.Descricao, comp.Ano, comp.Mes);
        if (servico.InformacoesComplementares is { } info)
            servico.InformacoesComplementares = ModeloDescricao.Aplicar(info, comp.Ano, comp.Mes);
        return new Nota
        {
            CompetenciaId = comp.Id,
            RecorrenciaId = r.Id,
            Origem = OrigemNota.Recorrencia,
            Identificacao = r.Identificacao,
            Tomador = r.Tomador.Copiar(),
            Servico = servico,
            CamposVariaveis = r.CamposVariaveis,
            CamposRevisados = CamposVariaveis.Nenhum,
            Valor = null,
            Situacao = SituacaoNota.Pendente,
            CenarioSimulacao = CenarioSimulacao.Sucesso,
            CriadaEm = agora,
            AtualizadaEm = agora,
        };
    }

    public async Task<CompetenciaDto> ObterAsync(int ano, int mes, CancellationToken ct)
    {
        ValidarMes(ano, mes);
        var ativas = await db.Recorrencias.CountAsync(r => r.Ativa, ct);
        var comp = await db.Competencias.AsNoTracking().SingleOrDefaultAsync(c => c.Ano == ano && c.Mes == mes, ct);
        if (comp is null)
            return new CompetenciaDto(ano, mes, false, null, null, ativas, ativas, [], []);

        var notas = await db.Notas.AsNoTracking().Where(n => n.CompetenciaId == comp.Id)
            .OrderBy(n => n.Tomador.Nome).ThenBy(n => n.Id).ToListAsync(ct);
        var tentativas = await db.Tentativas.AsNoTracking().Where(t => notas.Select(n => n.Id).Contains(t.NotaId))
            .GroupBy(t => t.NotaId).Select(g => new { NotaId = g.Key, Qtd = g.Count() }).ToDictionaryAsync(x => x.NotaId, x => x.Qtd, ct);
        var recIds = notas.Where(n => n.RecorrenciaId != null).Select(n => n.RecorrenciaId!.Value).ToList();
        var recAtivas = await db.Recorrencias.AsNoTracking().Where(r => recIds.Contains(r.Id) && r.Ativa).Select(r => r.Id).ToListAsync(ct);
        var semLinha = await db.Recorrencias.CountAsync(r => r.Ativa && !recIds.Contains(r.Id), ct);
        var anteriores = await ValoresAnterioresAsync(db, recIds, ano, mes, ct);

        var linhas = notas.Select(n => new LinhaDto(
            n.Id, n.Origem, n.RecorrenciaId, n.RecorrenciaId is { } rid && recAtivas.Contains(rid), n.Identificacao,
            n.Tomador.Nome, n.Tomador.Documento, n.Servico.Descricao, n.Valor,
            n.RecorrenciaId is { } r2 && anteriores.TryGetValue(r2, out var va) ? va : null,
            n.Situacao, n.UltimaMensagem, Pendencias(n), n.CamposVariaveis, n.CamposPendentesDeRevisao,
            n.CenarioSimulacao, n.NumeroNfse, n.EmitidaEm, tentativas.GetValueOrDefault(n.Id), n.Versao)).ToList();

        var resumo = notas.GroupBy(n => n.Situacao)
            .Select(g => new ResumoSituacaoDto(g.Key, g.Count(), g.Sum(n => n.Valor ?? 0)))
            .OrderBy(r => r.Situacao).ToList();

        return new CompetenciaDto(ano, mes, true, comp.Id, comp.AbertaEm, ativas, semLinha, linhas, resumo);
    }

    internal static List<string> Pendencias(Nota n) =>
        n.Situacao is SituacaoNota.Pendente or SituacaoNota.Erro ? Validadores.ValidarNotaParaPronta(n) : [];

    /// <summary>
    /// Valor da competência anterior: o último valor preenchido da mesma recorrência em uma competência
    /// anterior (desconsiderando linhas ignoradas). Apenas referência; nunca é copiado automaticamente.
    /// </summary>
    internal static async Task<Dictionary<long, ValorAnteriorDto>> ValoresAnterioresAsync(
        AppDbContext db, List<long> recIds, int ano, int mes, CancellationToken ct)
    {
        if (recIds.Count == 0) return [];
        var chave = Competencia.ChaveDe(ano, mes);
        var anteriores = await db.Notas.AsNoTracking()
            .Where(n => n.RecorrenciaId != null && recIds.Contains(n.RecorrenciaId.Value) && n.Valor != null
                        && n.Situacao != SituacaoNota.Ignorada
                        && n.Competencia!.Ano * 100 + n.Competencia.Mes < chave)
            .Select(n => new { Rec = n.RecorrenciaId!.Value, n.Competencia!.Ano, n.Competencia.Mes, Valor = n.Valor!.Value, n.Situacao })
            .ToListAsync(ct);
        return anteriores.GroupBy(a => a.Rec).ToDictionary(g => g.Key, g =>
        {
            var u = g.OrderByDescending(x => x.Ano * 100 + x.Mes).First();
            return new ValorAnteriorDto(u.Valor, u.Ano, u.Mes, u.Situacao);
        });
    }
}
