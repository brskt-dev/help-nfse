using HelpNfse.Core.Dados;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Validacao;
using Microsoft.EntityFrameworkCore;

namespace HelpNfse.Core.Servicos;

public sealed class EmissorServico(AppDbContext db, TimeProvider relogio)
{
    public async Task<EmissorDto> ObterAsync(CancellationToken ct)
    {
        var e = await db.Emissores.AsNoTracking().SingleOrDefaultAsync(x => x.Id == Emissor.IdUnico, ct);
        return ParaDto(e);
    }

    public async Task<EmissorDto> SalvarAsync(SalvarEmissorDto dto, CancellationToken ct)
    {
        var e = await db.Emissores.SingleOrDefaultAsync(x => x.Id == Emissor.IdUnico, ct);
        var novo = e is null;
        e ??= new Emissor();

        e.RazaoSocial = (dto.RazaoSocial ?? "").Trim();
        e.NomeFantasia = Formatos.Limpar(dto.NomeFantasia);
        e.Cnpj = DocumentoFiscal.Normalizar(dto.Cnpj);
        e.InscricaoMunicipal = Formatos.Limpar(dto.InscricaoMunicipal);
        e.Email = Formatos.Limpar(dto.Email);
        e.Telefone = Formatos.Limpar(dto.Telefone);
        e.Endereco = Validadores.NormalizarEndereco(dto.Endereco ?? new());
        e.OpcaoSimplesNacional = dto.OpcaoSimplesNacional;
        e.RegimeApuracaoSimples = dto.RegimeApuracaoSimples;
        e.RegimeEspecialTributacao = dto.RegimeEspecialTributacao;
        e.SerieDps = Formatos.ApenasDigitos(dto.SerieDps);
        e.ProximoNumeroDps = dto.ProximoNumeroDps;
        e.RegraDataCompetencia = dto.RegraDataCompetencia;
        e.AtualizadoEm = relogio.GetUtcNow();

        var erros = Validadores.ValidarEmissor(e);
        if (erros.Count > 0) throw new ErroNegocio("Verifique os dados do emissor.", 400, erros);

        if (!novo)
        {
            // O número da DPS nunca pode retroceder abaixo de um número já atribuído.
            var maiorAtribuido = await db.Notas.Where(n => n.SerieDps == e.SerieDps && n.NumeroDps != null)
                .MaxAsync(n => (long?)n.NumeroDps, ct);
            if (maiorAtribuido is { } m && e.ProximoNumeroDps <= m)
                throw new ErroNegocio($"O próximo número da DPS deve ser maior que {m}, o último número já atribuído na série {e.SerieDps}.");
        }

        if (novo) db.Emissores.Add(e);
        await db.SaveChangesAsync(ct);
        return ParaDto(e);
    }

    internal static EmissorDto ParaDto(Emissor? e) => e is null
        ? new EmissorDto(false, "", null, "", null, null, null, new Endereco(), OpcaoSimplesNacional.NaoInformado,
            RegimeApuracaoSimples.NaoInformado, RegimeEspecialTributacao.NaoInformado, "1", 1,
            RegraDataCompetencia.NaoDefinida, Validadores.PendenciasDeConfirmacao(null), null)
        : new EmissorDto(true, e.RazaoSocial, e.NomeFantasia, e.Cnpj, e.InscricaoMunicipal, e.Email, e.Telefone,
            e.Endereco, e.OpcaoSimplesNacional, e.RegimeApuracaoSimples, e.RegimeEspecialTributacao, e.SerieDps,
            e.ProximoNumeroDps, e.RegraDataCompetencia, Validadores.PendenciasDeConfirmacao(e), e.AtualizadoEm);
}
