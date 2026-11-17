namespace HelpNfse.Core.Dominio;

/// <summary>Prestador (emissor) único do sistema.</summary>
public class Emissor
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;
    public string RazaoSocial { get; set; } = "";
    public string? NomeFantasia { get; set; }
    public string Cnpj { get; set; } = "";
    public string? InscricaoMunicipal { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public Endereco Endereco { get; set; } = new();

    public OpcaoSimplesNacional OpcaoSimplesNacional { get; set; }
    public RegimeApuracaoSimples RegimeApuracaoSimples { get; set; }
    public RegimeEspecialTributacao RegimeEspecialTributacao { get; set; }

    /// <summary>Série da DPS. A confirmar no leiaute oficial e com o contador.</summary>
    public string SerieDps { get; set; } = "1";
    /// <summary>Próximo número de DPS a ser atribuído. Cada nota recebe um número uma única vez.</summary>
    public long ProximoNumeroDps { get; set; } = 1;
    public RegraDataCompetencia RegraDataCompetencia { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }
}

/// <summary>Cadastro de uma cobrança recorrente mensal.</summary>
public class Recorrencia
{
    public long Id { get; set; }
    public string Identificacao { get; set; } = "";
    public bool Ativa { get; set; } = true;
    public DadosTomador Tomador { get; set; } = new();
    public DadosServico Servico { get; set; } = new();
    public CamposVariaveis CamposVariaveis { get; set; }
    public string? Observacoes { get; set; }
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset AtualizadaEm { get; set; }
    public DateTimeOffset? DesativadaEm { get; set; }
    public uint Versao { get; set; }
}

/// <summary>Mês de referência (competência) aberto no sistema.</summary>
public class Competencia
{
    public long Id { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public DateTimeOffset AbertaEm { get; set; }
    public List<Nota> Notas { get; set; } = [];

    public int Chave => ChaveDe(Ano, Mes);
    public static int ChaveDe(int ano, int mes) => ano * 100 + mes;
    public string Rotulo => $"{Mes:00}/{Ano}";
}

/// <summary>
/// Linha de uma competência: uma nota a emitir. Guarda uma cópia (snapshot) dos dados usados na competência,
/// independente de alterações posteriores no cadastro.
/// </summary>
public class Nota
{
    public long Id { get; set; }
    public long CompetenciaId { get; set; }
    public Competencia? Competencia { get; set; }
    public long? RecorrenciaId { get; set; }
    public Recorrencia? Recorrencia { get; set; }
    public OrigemNota Origem { get; set; }

    public string Identificacao { get; set; } = "";
    public DadosTomador Tomador { get; set; } = new();
    public DadosServico Servico { get; set; } = new();
    public CamposVariaveis CamposVariaveis { get; set; }
    public CamposVariaveis CamposRevisados { get; set; }

    public decimal? Valor { get; set; }
    public SituacaoNota Situacao { get; set; } = SituacaoNota.Pendente;
    public string? MotivoIgnorada { get; set; }
    public CenarioSimulacao CenarioSimulacao { get; set; }

    // Identificação da DPS: atribuída uma única vez, no primeiro pedido de emissão, e reutilizada.
    public string? SerieDps { get; set; }
    public long? NumeroDps { get; set; }
    public string? IdentificadorDps { get; set; }
    public DateOnly? DataCompetencia { get; set; }
    /// <summary>Dados congelados da DPS (prestador, tomador, serviço, valores) no momento da confirmação.</summary>
    public string? DpsJson { get; set; }

    // Resultado da emissão
    public string? NumeroNfse { get; set; }
    public string? ChaveAcesso { get; set; }
    public DateTimeOffset? EmitidaEm { get; set; }
    public bool EmissaoSimulada { get; set; }
    /// <summary>XML autorizado retornado pelo provedor real. Nunca preenchido na simulação.</summary>
    public string? XmlAutorizado { get; set; }

    public string? UltimaMensagem { get; set; }
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset AtualizadaEm { get; set; }
    public uint Versao { get; set; }

    public List<TentativaEmissao> Tentativas { get; set; } = [];

    public bool EstaBloqueadaParaEdicao =>
        Situacao is SituacaoNota.Processando or SituacaoNota.Emitida or SituacaoNota.ResultadoAConfirmar;

    /// <summary>Campos variáveis que ainda precisam de revisão nesta competência.</summary>
    public CamposVariaveis CamposPendentesDeRevisao => CamposVariaveis & ~CamposRevisados;
}

/// <summary>Pedido de emissão confirmado pelo usuário (um clique em "Emitir notas prontas").</summary>
public class LoteEmissao
{
    public long Id { get; set; }
    public long CompetenciaId { get; set; }
    public Guid ChaveIdempotencia { get; set; }
    public int Quantidade { get; set; }
    public decimal ValorTotal { get; set; }
    public string? SolicitadoPor { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
}

/// <summary>Item da fila de processamento (banco de dados como fila).</summary>
public class TrabalhoEmissao
{
    public long Id { get; set; }
    public long NotaId { get; set; }
    public Nota? Nota { get; set; }
    public long? LoteId { get; set; }
    public StatusTrabalho Status { get; set; }
    public DateTimeOffset ProximaExecucaoEm { get; set; }
    public string? BloqueadoPor { get; set; }
    public DateTimeOffset? BloqueadoAte { get; set; }
    public Guid? TokenBloqueio { get; set; }
    public int Execucoes { get; set; }
    public int FalhasTemporarias { get; set; }
    public int ConsultasIndisponiveis { get; set; }
    public string? UltimaMensagem { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
    public DateTimeOffset? ConcluidoEm { get; set; }
}

public sealed record MensagemProvedor(string Codigo, string Descricao, string? Complemento = null);

/// <summary>Registro histórico de cada envio ou consulta feito ao provedor. Nunca é apagado.</summary>
public class TentativaEmissao
{
    public long Id { get; set; }
    public long NotaId { get; set; }
    public long? TrabalhoId { get; set; }
    public int Sequencia { get; set; }
    public TipoTentativa Tipo { get; set; }
    public ResultadoTentativa Resultado { get; set; }
    public DateTimeOffset IniciadaEm { get; set; }
    public DateTimeOffset? FinalizadaEm { get; set; }
    public string? IdentificadorDps { get; set; }
    public string Provedor { get; set; } = "";
    public string? WorkerId { get; set; }
    public string? Detalhe { get; set; }
    public List<MensagemProvedor> Mensagens { get; set; } = [];
    /// <summary>Cópia dos dados enviados (somente em envios).</summary>
    public string? DadosEnviadosJson { get; set; }
    public string? NumeroNfse { get; set; }
    public string? ChaveAcesso { get; set; }
}

/// <summary>Sinal de vida do worker, para exibição na interface.</summary>
public class StatusWorker
{
    public string WorkerId { get; set; } = "";
    public DateTimeOffset IniciadoEm { get; set; }
    public DateTimeOffset UltimoSinal { get; set; }
    public string? Provedor { get; set; }
}
