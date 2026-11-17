using System.Security.Claims;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Documentos;
using HelpNfse.Core.Emissao;
using HelpNfse.Core.Servicos;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Api.Endpoints;

public static class CompetenciaEndpoints
{
    public static void MapCompetenciaEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/competencias");
        g.MapGet("", (CompetenciaServico s, CancellationToken ct) => s.ListarAsync(ct));
        g.MapGet("/{ano:int}/{mes:int}", (int ano, int mes, CompetenciaServico s, CancellationToken ct) => s.ObterAsync(ano, mes, ct));
        g.MapPost("/{ano:int}/{mes:int}/abrir", (int ano, int mes, CompetenciaServico s, CancellationToken ct) => s.AbrirAsync(ano, mes, ct));

        g.MapGet("/{id:long}/emissao/previa", (long id, EmissaoServico s, CancellationToken ct) => s.PreviaAsync(id, ct));
        g.MapPost("/{id:long}/emissao", (long id, SolicitarEmissaoDto dto, EmissaoServico s, ClaimsPrincipal user, CancellationToken ct) =>
            s.SolicitarAsync(id, dto, user.Identity?.Name, ct));
        g.MapPost("/{id:long}/avulsas", (long id, CriarAvulsaDto dto, NotaServico s, CancellationToken ct) => s.CriarAvulsaAsync(id, dto, ct));
    }

    public static void MapNotaEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/notas");
        g.MapGet("/{id:long}", (long id, NotaServico s, CancellationToken ct) => s.ObterAsync(id, ct));
        g.MapPut("/{id:long}", (long id, AtualizarNotaDto dto, NotaServico s, CancellationToken ct) => s.AtualizarAsync(id, dto, ct));
        g.MapPut("/{id:long}/valor", (long id, AtualizarValorDto dto, NotaServico s, CancellationToken ct) => s.AtualizarValorAsync(id, dto, ct));
        g.MapDelete("/{id:long}", async (long id, NotaServico s, CancellationToken ct) =>
        {
            await s.ExcluirAvulsaAsync(id, ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:long}/atualizar-cadastro", (long id, NotaServico s, CancellationToken ct) => s.AtualizarDoCadastroAsync(id, ct));
        g.MapPost("/{id:long}/consultar", async (long id, NotaServico s, CancellationToken ct) =>
        {
            await s.SolicitarConsultaAsync(id, ct);
            return Results.Accepted();
        });

        g.MapPost("/copiar-valor-anterior", (CopiarValorAnteriorDto dto, NotaServico s, CancellationToken ct) => s.CopiarValorAnteriorAsync(dto, ct));
        g.MapPost("/marcar-prontas", (IdsDto dto, NotaServico s, CancellationToken ct) => s.MarcarProntasAsync(dto.Ids, ct));
        g.MapPost("/voltar-pendente", (IdsDto dto, NotaServico s, CancellationToken ct) => s.VoltarParaPendenteAsync(dto.Ids, ct));
        g.MapPost("/ignorar", (IgnorarDto dto, NotaServico s, CancellationToken ct) => s.IgnorarAsync(dto, ct));
        g.MapPost("/restaurar", (IdsDto dto, NotaServico s, CancellationToken ct) => s.RestaurarAsync(dto.Ids, ct));

        // Documentos: sempre autenticados (política padrão) e gerados a partir dos dados congelados da nota.
        g.MapGet("/{id:long}/pdf", async (long id, AppDbContext db, TimeProvider relogio, CancellationToken ct) =>
        {
            var n = await db.Notas.AsNoTracking().Include(x => x.Tentativas).SingleOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw ErroNegocio.NaoEncontrado("Nota");
            var pdf = GeradorPdf.GerarSimulacao(n, n.Tentativas, relogio.GetUtcNow());
            return Results.File(pdf, "application/pdf", $"SIMULACAO-sem-validade-fiscal-{n.NumeroNfse}.pdf");
        });
        g.MapGet("/{id:long}/xml", async (long id, AppDbContext db, CancellationToken ct) =>
        {
            var n = await db.Notas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ErroNegocio.NaoEncontrado("Nota");
            if (n.XmlAutorizado is null)
                throw new ErroNegocio(n.EmissaoSimulada
                    ? "Notas simuladas não possuem XML fiscal."
                    : "XML autorizado não disponível para esta nota.", 404);
            return Results.File(System.Text.Encoding.UTF8.GetBytes(n.XmlAutorizado), "application/xml", $"nfse-{n.ChaveAcesso}.xml");
        });
        g.MapGet("/{id:long}/tentativas/{tid:long}/dados", async (long id, long tid, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.Tentativas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tid && x.NotaId == id, ct)
                    ?? throw ErroNegocio.NaoEncontrado("Tentativa");
            return t.DadosEnviadosJson is null
                ? Results.Problem("Tentativa sem dados enviados.", statusCode: 404)
                : Results.Text(t.DadosEnviadosJson, "application/json");
        });
    }
}
