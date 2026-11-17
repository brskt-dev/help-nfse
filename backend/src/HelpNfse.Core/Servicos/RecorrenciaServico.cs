using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Validacao;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HelpNfse.Core.Servicos;

public sealed class RecorrenciaServico(AppDbContext db, TimeProvider relogio)
{
    public async Task<List<RecorrenciaResumoDto>> ListarAsync(bool incluirInativas, CancellationToken ct)
    {
        var q = db.Recorrencias.AsNoTracking();
        if (!incluirInativas) q = q.Where(r => r.Ativa);
        var lista = await q.OrderByDescending(r => r.Ativa).ThenBy(r => r.Identificacao).ToListAsync(ct);
        return lista.Select(r => new RecorrenciaResumoDto(r.Id, r.Identificacao, r.Ativa, r.Tomador.Nome,
            r.Tomador.Documento, r.Servico.Descricao, r.CamposVariaveis, r.ReferenciaServico, r.AtualizadaEm)).ToList();
    }

    public async Task<RecorrenciaDto> ObterAsync(long id, CancellationToken ct)
    {
        var r = await db.Recorrencias.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ErroNegocio.NaoEncontrado("Recorrência");
        var historico = await db.Notas.AsNoTracking()
            .Where(n => n.RecorrenciaId == id)
            .OrderByDescending(n => n.Competencia!.Ano).ThenByDescending(n => n.Competencia!.Mes)
            .Select(n => new HistoricoRecorrenciaDto(n.Id, n.Competencia!.Ano, n.Competencia.Mes, n.MesReferencia, n.Valor, n.Situacao,
                n.NumeroNfse, n.EmitidaEm))
            .ToListAsync(ct);
        return new RecorrenciaDto(r.Id, r.Identificacao, r.Ativa, r.Tomador, r.Servico, r.CamposVariaveis,
            r.ReferenciaServico, r.Observacoes, r.CriadaEm, r.AtualizadaEm, r.DesativadaEm, r.Versao, historico);
    }

    public async Task<RecorrenciaDto> CriarAsync(SalvarRecorrenciaDto dto, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var r = new Recorrencia { Ativa = true, CriadaEm = agora };
        Aplicar(r, dto, agora);
        db.Recorrencias.Add(r);
        await SalvarAsync(ct);
        return await ObterAsync(r.Id, ct);
    }

    public async Task<RecorrenciaDto> AtualizarAsync(long id, SalvarRecorrenciaDto dto, CancellationToken ct)
    {
        var r = await db.Recorrencias.SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ErroNegocio.NaoEncontrado("Recorrência");
        if (dto.Versao is { } v) db.Entry(r).Property(x => x.Versao).OriginalValue = v;
        Aplicar(r, dto, relogio.GetUtcNow());
        await SalvarAsync(ct);
        return await ObterAsync(r.Id, ct);
    }

    /// <summary>Desativa sem apagar: linhas e notas já geradas continuam no histórico.</summary>
    public async Task DefinirAtivaAsync(long id, bool ativa, CancellationToken ct)
    {
        var r = await db.Recorrencias.SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ErroNegocio.NaoEncontrado("Recorrência");
        var agora = relogio.GetUtcNow();
        r.Ativa = ativa;
        r.DesativadaEm = ativa ? null : agora;
        r.AtualizadaEm = agora;
        await db.SaveChangesAsync(ct);
    }

    private static void Aplicar(Recorrencia r, SalvarRecorrenciaDto dto, DateTimeOffset agora)
    {
        r.Identificacao = (dto.Identificacao ?? "").Trim();
        r.Tomador = Validadores.NormalizarTomador(dto.Tomador ?? new());
        r.Servico = Validadores.NormalizarServico(dto.Servico ?? new());
        r.CamposVariaveis = dto.CamposVariaveis;
        r.ReferenciaServico = dto.ReferenciaServico;
        r.Observacoes = Formatos.Limpar(dto.Observacoes);
        r.AtualizadaEm = agora;

        var erros = Validadores.ValidarRecorrencia(r);
        if (erros.Count > 0) throw new ErroNegocio("Verifique os dados da recorrência.", 400, erros);
    }

    private async Task SalvarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ErroNegocio.Conflito("A recorrência foi alterada em outra tela. Recarregue e tente novamente.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ErroNegocio("Já existe uma recorrência com esta identificação.");
        }
    }
}
