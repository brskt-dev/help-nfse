namespace HelpNfse.Core.Dominio;

/// <summary>Situação de uma nota (linha) dentro de uma competência.</summary>
public enum SituacaoNota
{
    Pendente,
    Pronta,
    Processando,
    Emitida,
    Erro,
    ResultadoAConfirmar,
    Ignorada,
}

public enum OrigemNota
{
    Recorrencia,
    Avulsa,
}

/// <summary>
/// Campos que podem variar a cada competência, além do valor (que é sempre variável).
/// Um campo marcado como variável é editável na competência e precisa ser revisado antes de a linha ficar pronta.
/// </summary>
[Flags]
public enum CamposVariaveis
{
    Nenhum = 0,
    Descricao = 1,
    InformacoesComplementares = 2,
}

/// <summary>
/// Opção pelo Simples Nacional do prestador. Códigos fiscais exatos devem ser conferidos no leiaute oficial da DPS
/// e confirmados com o contador antes da homologação.
/// </summary>
public enum OpcaoSimplesNacional
{
    NaoInformado,
    NaoOptante,
    Mei,
    MeEpp,
}

/// <summary>Regime de apuração dos tributos para optantes ME/EPP do Simples Nacional. A confirmar com o contador.</summary>
public enum RegimeApuracaoSimples
{
    NaoInformado,
    TributosFederaisEMunicipalPeloSimples,
    TributosFederaisPeloSimplesIssqnFora,
    TributosFederaisEMunicipalFora,
}

/// <summary>Regime especial de tributação municipal. A confirmar com o contador.</summary>
public enum RegimeEspecialTributacao
{
    NaoInformado,
    Nenhum,
    AtoCooperado,
    Estimativa,
    MicroempresaMunicipal,
    NotarioOuRegistrador,
    ProfissionalAutonomo,
    SociedadeDeProfissionais,
}

/// <summary>Regra para definir a data de competência informada na DPS. A confirmar com o contador.</summary>
public enum RegraDataCompetencia
{
    NaoDefinida,
    UltimoDiaDoMes,
    PrimeiroDiaDoMes,
    DataDaEmissao,
}

/// <summary>Tributação do ISSQN da operação. A confirmar com o contador.</summary>
public enum TributacaoIssqn
{
    NaoInformado,
    OperacaoTributavel,
    Imunidade,
    ExportacaoDeServico,
    NaoIncidencia,
}

/// <summary>
/// Cenário a ser reproduzido pelo provedor simulado. Ignorado por provedores reais.
/// </summary>
public enum CenarioSimulacao
{
    Sucesso,
    RejeicaoFiscal,
    FalhaTemporaria,
    FalhaTemporariaPersistente,
    TimeoutEmitida,
    TimeoutNaoEmitida,
    ConsultaIndisponivel,
    InterromperWorker,
}

public enum TipoTentativa
{
    Envio,
    Consulta,
}

public enum ResultadoTentativa
{
    /// <summary>Registrada antes da chamada ao provedor; ainda sem resposta.</summary>
    EmAndamento,
    Autorizada,
    Rejeitada,
    /// <summary>Falha com certeza de que o provedor não processou a requisição.</summary>
    FalhaTemporaria,
    /// <summary>Requisição pode ter sido processada (ex.: timeout após envio).</summary>
    Incerta,
    /// <summary>O worker foi interrompido durante o envio; tratada como incerta.</summary>
    Interrompida,
    /// <summary>Provedor informou que a DPS já havia sido processada; exige consulta.</summary>
    DuplicidadeDetectada,
    Encontrada,
    NaoEncontrada,
    ConsultaIndisponivel,
}

public enum StatusTrabalho
{
    Pendente,
    EmExecucao,
    Concluido,
    /// <summary>Aguardando ação do usuário (ex.: consultas esgotadas com resultado ainda incerto).</summary>
    Suspenso,
    Cancelado,
}
