using HelpNfse.Core.Configuracao;
using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpNfse.Api.Endpoints;

public static class CadastroEndpoints
{
    public static void MapSistemaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sistema", async (AppDbContext db, IProvedorNfse provedor, IOptions<OpcoesCertificado> cert,
            TimeProvider relogio, CancellationToken ct) =>
        {
            var agora = relogio.GetUtcNow();
            var workers = await db.StatusWorkers.AsNoTracking().OrderByDescending(w => w.UltimoSinal).Take(5).ToListAsync(ct);
            var pendentes = await db.Trabalhos.CountAsync(t => t.Status == StatusTrabalho.Pendente || t.Status == StatusTrabalho.EmExecucao, ct);
            var versao = Environment.GetEnvironmentVariable("APP_VERSION") ?? typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev";
            return new SistemaDto(provedor.Nome, provedor.Simulado,
                provedor.Simulado ? "SIMULAÇÃO — SEM VALIDADE FISCAL" : "",
                cert.Value.Configurado,
                workers.Select(w => new StatusWorkerDto(w.WorkerId, w.IniciadoEm, w.UltimoSinal, agora - w.UltimoSinal < TimeSpan.FromSeconds(45))).ToList(),
                pendentes, versao);
        });
    }

    public static void MapEmissorEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/emissor");
        g.MapGet("", (EmissorServico s, CancellationToken ct) => s.ObterAsync(ct));
        g.MapPut("", (SalvarEmissorDto dto, EmissorServico s, CancellationToken ct) => s.SalvarAsync(dto, ct));
    }

    public static void MapRecorrenciaEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/recorrencias");
        g.MapGet("", (bool? incluirInativas, RecorrenciaServico s, CancellationToken ct) => s.ListarAsync(incluirInativas ?? true, ct));
        g.MapGet("/{id:long}", (long id, RecorrenciaServico s, CancellationToken ct) => s.ObterAsync(id, ct));
        g.MapPost("", async (SalvarRecorrenciaDto dto, RecorrenciaServico s, CancellationToken ct) =>
        {
            var r = await s.CriarAsync(dto, ct);
            return Results.Created($"/api/recorrencias/{r.Id}", r);
        });
        g.MapPut("/{id:long}", (long id, SalvarRecorrenciaDto dto, RecorrenciaServico s, CancellationToken ct) => s.AtualizarAsync(id, dto, ct));
        g.MapPost("/{id:long}/desativar", async (long id, RecorrenciaServico s, CancellationToken ct) =>
        {
            await s.DefinirAtivaAsync(id, false, ct);
            return Results.NoContent();
        });
        g.MapPost("/{id:long}/reativar", async (long id, RecorrenciaServico s, CancellationToken ct) =>
        {
            await s.DefinirAtivaAsync(id, true, ct);
            return Results.NoContent();
        });
    }
}
