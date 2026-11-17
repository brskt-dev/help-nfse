namespace HelpNfse.Core.Dominio;

/// <summary>Endereço (complex type, gravado em colunas da entidade dona).</summary>
public sealed record Endereco
{
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    /// <summary>Código IBGE do município (7 dígitos).</summary>
    public string? CodigoMunicipio { get; set; }
    public string? NomeMunicipio { get; set; }
    public string? Uf { get; set; }

    public Endereco Copiar() => this with { };
}

/// <summary>Dados do tomador do serviço.</summary>
public sealed record DadosTomador
{
    /// <summary>CPF (11) ou CNPJ (14, numérico ou alfanumérico), sem pontuação.</summary>
    public string Documento { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? InscricaoMunicipal { get; set; }
    public Endereco Endereco { get; set; } = new();

    public DadosTomador Copiar() => this with { Endereco = Endereco.Copiar() };
}

/// <summary>Dados do serviço e parâmetros fiscais conhecidos.</summary>
public sealed record DadosServico
{
    public string Descricao { get; set; } = "";
    /// <summary>Código de tributação nacional (cTribNac). A confirmar com o contador.</summary>
    public string? CodigoTributacaoNacional { get; set; }
    /// <summary>Código de tributação municipal, quando aplicável. A confirmar com o contador.</summary>
    public string? CodigoTributacaoMunicipal { get; set; }
    /// <summary>Código NBS, quando aplicável. A confirmar com o contador.</summary>
    public string? CodigoNbs { get; set; }
    /// <summary>Código IBGE do município de prestação/incidência. A confirmar com o contador.</summary>
    public string? CodigoMunicipioPrestacao { get; set; }
    public TributacaoIssqn TributacaoIssqn { get; set; }
    /// <summary>Alíquota do ISS em percentual, quando aplicável. Informativa; o sistema não calcula tributos.</summary>
    public decimal? AliquotaIss { get; set; }
    public bool IssRetido { get; set; }
    public string? InformacoesComplementares { get; set; }

    public DadosServico Copiar() => this with { };
}
