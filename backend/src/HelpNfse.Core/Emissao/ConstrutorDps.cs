using HelpNfse.Core.Dominio;
using HelpNfse.Core.Provedores;
using HelpNfse.Core.Servicos;

namespace HelpNfse.Core.Emissao;

/// <summary>Monta os dados congelados da DPS a partir da linha da competência e do emissor.</summary>
public static class ConstrutorDps
{
    public static DateOnly CalcularDataCompetencia(RegraDataCompetencia regra, int ano, int mes, DateTimeOffset agoraUtc) => regra switch
    {
        RegraDataCompetencia.UltimoDiaDoMes => new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes)),
        RegraDataCompetencia.PrimeiroDiaDoMes => new DateOnly(ano, mes, 1),
        RegraDataCompetencia.DataDaEmissao => DateOnly.FromDateTime(Formatos.ParaBrasilia(agoraUtc).DateTime),
        _ => throw new ErroNegocio("Defina a regra da data de competência da DPS na configuração do emissor."),
    };

    public static PrestadorDps Prestador(Emissor e) => new()
    {
        Cnpj = e.Cnpj,
        RazaoSocial = e.RazaoSocial,
        NomeFantasia = e.NomeFantasia,
        InscricaoMunicipal = e.InscricaoMunicipal,
        Email = e.Email,
        Telefone = e.Telefone,
        Endereco = e.Endereco.Copiar(),
        OpcaoSimplesNacional = e.OpcaoSimplesNacional,
        RegimeApuracaoSimples = e.RegimeApuracaoSimples,
        RegimeEspecialTributacao = e.RegimeEspecialTributacao,
    };

    public static DadosDps Construir(Nota nota, Competencia comp, Emissor emissor, IProvedorNfse provedor,
        string serie, long numero, DateTimeOffset agoraUtc)
    {
        var prestador = Prestador(emissor);
        return new DadosDps
        {
            IdentificadorDps = provedor.GerarIdentificadorDps(prestador, serie, numero),
            Serie = serie,
            Numero = numero,
            DataCompetencia = CalcularDataCompetencia(emissor.RegraDataCompetencia, comp.Ano, comp.Mes, agoraUtc),
            CompetenciaAno = comp.Ano,
            CompetenciaMes = comp.Mes,
            ConfirmadaEm = agoraUtc,
            Prestador = prestador,
            Tomador = nota.Tomador.Copiar(),
            Servico = nota.Servico.Copiar(),
            Valor = nota.Valor ?? throw new InvalidOperationException("Nota sem valor."),
            Provedor = provedor.Nome,
            Simulacao = provedor.Simulado,
        };
    }
}
