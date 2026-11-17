using HelpNfse.Core.Dominio;

namespace HelpNfse.Core.Provedores;

/// <summary>
/// Contrato de integração com o serviço de emissão de NFS-e.
/// Nesta entrega existe apenas o provedor simulado. A integração com a NFS-e Nacional (Produção Restrita)
/// será um novo provedor implementando este contrato, após consulta à documentação oficial.
/// </summary>
public interface IProvedorNfse
{
    /// <summary>Nome técnico do provedor (ex.: "Simulacao").</summary>
    string Nome { get; }

    /// <summary>Indica se o provedor é uma simulação sem validade fiscal.</summary>
    bool Simulado { get; }

    /// <summary>
    /// Gera o identificador determinístico da DPS. Deve ser estável para (prestador, série, número),
    /// pois é usado para consultar/reconciliar emissões com resultado incerto.
    /// </summary>
    string GerarIdentificadorDps(PrestadorDps prestador, string serie, long numero);

    /// <summary>
    /// Envia a DPS. Implementações devem distinguir com cuidado:
    /// <list type="bullet">
    /// <item><see cref="ResultadoEnvio.FalhaTemporaria"/> somente quando há certeza de que a requisição NÃO foi processada;</item>
    /// <item><see cref="ResultadoEnvio.Incerto"/> quando a requisição pode ter sido processada (ex.: timeout após envio).</item>
    /// </list>
    /// </summary>
    Task<ResultadoEnvio> EmitirAsync(DadosDps dps, ContextoEnvio contexto, CancellationToken ct);

    /// <summary>Consulta se a DPS identificada gerou uma NFS-e.</summary>
    Task<ResultadoConsulta> ConsultarPorDpsAsync(string identificadorDps, CancellationToken ct);
}

/// <summary>Metadados de execução que não fazem parte da DPS. Provedores reais ignoram o cenário de simulação.</summary>
public sealed record ContextoEnvio(CenarioSimulacao CenarioSimulacao, string WorkerId);

public sealed record PrestadorDps
{
    public required string Cnpj { get; init; }
    public required string RazaoSocial { get; init; }
    public string? NomeFantasia { get; init; }
    public string? InscricaoMunicipal { get; init; }
    public string? Email { get; init; }
    public string? Telefone { get; init; }
    public required Endereco Endereco { get; init; }
    public OpcaoSimplesNacional OpcaoSimplesNacional { get; init; }
    public RegimeApuracaoSimples RegimeApuracaoSimples { get; init; }
    public RegimeEspecialTributacao RegimeEspecialTributacao { get; init; }
}

/// <summary>
/// Dados da DPS congelados no momento em que o usuário confirma a emissão.
/// Reenvios usam exatamente estes dados (mesmo identificador), o que permite a reconciliação.
/// </summary>
public sealed record DadosDps
{
    public required string IdentificadorDps { get; init; }
    public required string Serie { get; init; }
    public required long Numero { get; init; }
    public required DateOnly DataCompetencia { get; init; }
    public required int CompetenciaAno { get; init; }
    public required int CompetenciaMes { get; init; }
    public required DateTimeOffset ConfirmadaEm { get; init; }
    public required PrestadorDps Prestador { get; init; }
    public required DadosTomador Tomador { get; init; }
    public required DadosServico Servico { get; init; }
    public required decimal Valor { get; init; }
    public required string Provedor { get; init; }
    public required bool Simulacao { get; init; }
}

public sealed record NfseAutorizada(
    string NumeroNfse,
    string ChaveAcesso,
    DateTimeOffset EmitidaEm,
    string? XmlAutorizado,
    bool Simulada);

public abstract record ResultadoEnvio
{
    private ResultadoEnvio() { }

    public sealed record Autorizada(NfseAutorizada Nfse) : ResultadoEnvio;

    /// <summary>Rejeição conhecida: a DPS foi analisada e recusada. Nenhuma NFS-e foi gerada.</summary>
    public sealed record Rejeitada(IReadOnlyList<MensagemProvedor> Mensagens) : ResultadoEnvio;

    /// <summary>O provedor informa que a DPS já foi processada anteriormente. Exige consulta.</summary>
    public sealed record JaProcessada(string Detalhe) : ResultadoEnvio;

    /// <summary>Falha com certeza de não processamento (ex.: serviço indisponível antes do envio).</summary>
    public sealed record FalhaTemporaria(string Detalhe) : ResultadoEnvio;

    /// <summary>Resultado desconhecido: a requisição pode ter sido processada.</summary>
    public sealed record Incerto(string Detalhe) : ResultadoEnvio;
}

public abstract record ResultadoConsulta
{
    private ResultadoConsulta() { }

    public sealed record Encontrada(NfseAutorizada Nfse) : ResultadoConsulta;
    public sealed record NaoEncontrada(string Detalhe) : ResultadoConsulta;
    public sealed record Indisponivel(string Detalhe) : ResultadoConsulta;
}
