using HelpNfse.Core.Dominio;

namespace HelpNfse.Core.Servicos;

/// <summary>Rótulos em português para exibição em documentos.</summary>
public static class Rotulos
{
    public static string De(OpcaoSimplesNacional v) => v switch
    {
        OpcaoSimplesNacional.NaoOptante => "Não optante",
        OpcaoSimplesNacional.Mei => "Optante — MEI",
        OpcaoSimplesNacional.MeEpp => "Optante — ME/EPP",
        _ => "Não informado",
    };

    public static string De(RegimeEspecialTributacao v) => v switch
    {
        RegimeEspecialTributacao.Nenhum => "Nenhum",
        RegimeEspecialTributacao.AtoCooperado => "Ato cooperado",
        RegimeEspecialTributacao.Estimativa => "Estimativa",
        RegimeEspecialTributacao.MicroempresaMunicipal => "Microempresa municipal",
        RegimeEspecialTributacao.NotarioOuRegistrador => "Notário ou registrador",
        RegimeEspecialTributacao.ProfissionalAutonomo => "Profissional autônomo",
        RegimeEspecialTributacao.SociedadeDeProfissionais => "Sociedade de profissionais",
        _ => "Não informado",
    };

    public static string De(TributacaoIssqn v) => v switch
    {
        TributacaoIssqn.OperacaoTributavel => "Operação tributável",
        TributacaoIssqn.Imunidade => "Imunidade",
        TributacaoIssqn.ExportacaoDeServico => "Exportação de serviço",
        TributacaoIssqn.NaoIncidencia => "Não incidência",
        _ => "Não informado",
    };

    public static string De(TipoTentativa v) => v == TipoTentativa.Envio ? "Envio" : "Consulta";

    public static string De(ResultadoTentativa v) => v switch
    {
        ResultadoTentativa.EmAndamento => "Em andamento",
        ResultadoTentativa.Autorizada => "Autorizada",
        ResultadoTentativa.Rejeitada => "Rejeitada",
        ResultadoTentativa.FalhaTemporaria => "Falha temporária",
        ResultadoTentativa.Incerta => "Resultado incerto",
        ResultadoTentativa.Interrompida => "Interrompida",
        ResultadoTentativa.DuplicidadeDetectada => "Duplicidade detectada",
        ResultadoTentativa.Encontrada => "Encontrada na consulta",
        ResultadoTentativa.NaoEncontrada => "Não encontrada na consulta",
        ResultadoTentativa.ConsultaIndisponivel => "Consulta indisponível",
        _ => v.ToString(),
    };
}
