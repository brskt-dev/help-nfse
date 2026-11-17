using System.Text.RegularExpressions;
using HelpNfse.Core.Dominio;
using HelpNfse.Core.Servicos;

namespace HelpNfse.Core.Validacao;

public static partial class Validadores
{
    public const decimal ValorMaximo = 999_999_999.99m;
    public const int TamanhoMaximoDescricao = 2000;

    [GeneratedRegex(@"^\d{3}$")] private static partial Regex TresDigitos();
    [GeneratedRegex(@"^\d{6}$")] private static partial Regex SeisDigitos();
    [GeneratedRegex(@"^\d{7}$")] private static partial Regex SeteDigitos();
    [GeneratedRegex(@"^\d{8}$")] private static partial Regex OitoDigitos();
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")] private static partial Regex Email();
    [GeneratedRegex(@"^\d{1,5}$")] private static partial Regex Serie();
    [GeneratedRegex(@"\{[a-z_]+\}", RegexOptions.IgnoreCase)] private static partial Regex Marcador();

    public static string NomeCampo(CamposVariaveis campo) => campo switch
    {
        CamposVariaveis.Descricao => "Descrição do serviço",
        CamposVariaveis.InformacoesComplementares => "Informações complementares",
        _ => campo.ToString(),
    };

    public static IEnumerable<CamposVariaveis> Separar(CamposVariaveis campos) =>
        Enum.GetValues<CamposVariaveis>().Where(c => c != CamposVariaveis.Nenhum && campos.HasFlag(c));

    /// <summary>Regras para uma linha poder ser marcada como pronta.</summary>
    public static List<string> ValidarNotaParaPronta(Nota nota)
    {
        var erros = new List<string>();

        if (nota.Valor is null) erros.Add("Informe o valor desta competência.");
        else ValidarValor(nota.Valor.Value, erros);

        ValidarTomador(nota.Tomador, erros);
        ValidarServico(nota.Servico, erros, exigirCodigos: true);

        foreach (var campo in Separar(nota.CamposPendentesDeRevisao))
            erros.Add($"Revise o campo \"{NomeCampo(campo)}\" nesta competência.");

        return erros;
    }

    public static void ValidarValor(decimal valor, List<string> erros)
    {
        if (valor <= 0) erros.Add("O valor deve ser maior que zero.");
        else if (valor > ValorMaximo) erros.Add("O valor excede o limite permitido.");
        else if (decimal.Round(valor, 2) != valor) erros.Add("O valor deve ter no máximo duas casas decimais.");
    }

    public static void ValidarTomador(DadosTomador t, List<string> erros)
    {
        if (string.IsNullOrWhiteSpace(t.Documento)) erros.Add("Informe o CPF ou CNPJ do tomador.");
        else if (!DocumentoFiscal.Valido(t.Documento)) erros.Add("CPF/CNPJ do tomador inválido.");
        if (string.IsNullOrWhiteSpace(t.Nome)) erros.Add("Informe o nome ou razão social do tomador.");
        if (!string.IsNullOrWhiteSpace(t.Email) && !Email().IsMatch(t.Email)) erros.Add("E-mail do tomador inválido.");
        ValidarEndereco(t.Endereco, "do tomador", erros);
    }

    public static void ValidarEndereco(Endereco e, string sufixo, List<string> erros, bool obrigatorio = false)
    {
        var algum = new[] { e.Cep, e.Logradouro, e.Numero, e.Bairro, e.CodigoMunicipio, e.Uf }.Any(x => !string.IsNullOrWhiteSpace(x));
        if (!algum && !obrigatorio) return;
        if (!string.IsNullOrWhiteSpace(e.Cep) && !OitoDigitos().IsMatch(e.Cep)) erros.Add($"CEP {sufixo} deve ter 8 dígitos.");
        if (string.IsNullOrWhiteSpace(e.CodigoMunicipio)) erros.Add($"Informe o código IBGE do município {sufixo}.");
        else if (!SeteDigitos().IsMatch(e.CodigoMunicipio)) erros.Add($"Código IBGE do município {sufixo} deve ter 7 dígitos.");
        if (!string.IsNullOrWhiteSpace(e.Uf) && e.Uf.Length != 2) erros.Add($"UF {sufixo} deve ter 2 letras.");
    }

    public static void ValidarServico(DadosServico s, List<string> erros, bool exigirCodigos)
    {
        if (string.IsNullOrWhiteSpace(s.Descricao)) erros.Add("Informe a descrição do serviço.");
        else
        {
            if (s.Descricao.Length > TamanhoMaximoDescricao) erros.Add($"A descrição deve ter no máximo {TamanhoMaximoDescricao} caracteres.");
            if (exigirCodigos && Marcador().IsMatch(s.Descricao)) erros.Add("A descrição contém um marcador não substituído (ex.: {mes}).");
        }

        if (string.IsNullOrWhiteSpace(s.CodigoTributacaoNacional))
        {
            if (exigirCodigos) erros.Add("Informe o código de tributação nacional (confirmar com o contador).");
        }
        else if (!SeisDigitos().IsMatch(s.CodigoTributacaoNacional)) erros.Add("Código de tributação nacional deve ter 6 dígitos.");

        if (string.IsNullOrWhiteSpace(s.CodigoMunicipioPrestacao))
        {
            if (exigirCodigos) erros.Add("Informe o município de prestação (código IBGE).");
        }
        else if (!SeteDigitos().IsMatch(s.CodigoMunicipioPrestacao)) erros.Add("Código IBGE do município de prestação deve ter 7 dígitos.");

        if (s.AliquotaIss is { } a && (a < 0 || a > 100)) erros.Add("Alíquota do ISS deve estar entre 0 e 100%.");
        if (!string.IsNullOrWhiteSpace(s.CstIbsCbs) && !TresDigitos().IsMatch(s.CstIbsCbs)) erros.Add("CST do IBS/CBS deve ter 3 dígitos.");
        if (!string.IsNullOrWhiteSpace(s.ClassificacaoTributariaIbsCbs) && !SeisDigitos().IsMatch(s.ClassificacaoTributariaIbsCbs))
            erros.Add("Classificação tributária do IBS/CBS (cClassTrib) deve ter 6 dígitos.");
        if (!string.IsNullOrWhiteSpace(s.IndicadorOperacaoIbsCbs) && !SeisDigitos().IsMatch(s.IndicadorOperacaoIbsCbs))
            erros.Add("Indicador de operação do IBS/CBS deve ter 6 dígitos.");
        if (s.InformacoesComplementares?.Length > TamanhoMaximoDescricao)
            erros.Add($"Informações complementares devem ter no máximo {TamanhoMaximoDescricao} caracteres.");
    }

    public static List<string> ValidarRecorrencia(Recorrencia r)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Identificacao)) erros.Add("Informe a identificação interna.");
        ValidarTomador(r.Tomador, erros);
        ValidarServico(r.Servico, erros, exigirCodigos: false);
        return erros;
    }

    public static List<string> ValidarEmissor(Emissor e)
    {
        var erros = new List<string>();
        if (string.IsNullOrWhiteSpace(e.RazaoSocial)) erros.Add("Informe a razão social.");
        if (!DocumentoFiscal.CnpjValido(e.Cnpj)) erros.Add("CNPJ do emissor inválido.");
        if (!string.IsNullOrWhiteSpace(e.Email) && !Email().IsMatch(e.Email)) erros.Add("E-mail do emissor inválido.");
        ValidarEndereco(e.Endereco, "do emissor", erros, obrigatorio: true);
        if (!Serie().IsMatch(e.SerieDps ?? "")) erros.Add("Série da DPS deve ter de 1 a 5 dígitos.");
        if (e.ProximoNumeroDps < 1) erros.Add("Próximo número da DPS deve ser maior que zero.");
        return erros;
    }

    /// <summary>Requisitos mínimos do emissor para confirmar uma emissão (mesmo simulada).</summary>
    public static List<string> ValidarEmissorParaEmissao(Emissor? e)
    {
        if (e is null) return ["Configure os dados do emissor antes de emitir."];
        var erros = ValidarEmissor(e);
        if (e.RegraDataCompetencia == RegraDataCompetencia.NaoDefinida)
            erros.Add("Defina a regra da data de competência da DPS na configuração do emissor (confirmar com o contador).");
        return erros;
    }

    /// <summary>Campos do emissor que ainda dependem de confirmação com o contador.</summary>
    public static List<string> PendenciasDeConfirmacao(Emissor? e)
    {
        var p = new List<string>();
        if (e is null) return ["Dados do emissor não configurados."];
        if (e.OpcaoSimplesNacional == OpcaoSimplesNacional.NaoInformado) p.Add("Opção pelo Simples Nacional não informada.");
        if (e.OpcaoSimplesNacional == OpcaoSimplesNacional.MeEpp && e.RegimeApuracaoSimples == RegimeApuracaoSimples.NaoInformado)
            p.Add("Regime de apuração do Simples Nacional não informado.");
        if (e.RegimeEspecialTributacao == RegimeEspecialTributacao.NaoInformado) p.Add("Regime especial de tributação não informado.");
        if (e.RegraDataCompetencia == RegraDataCompetencia.NaoDefinida) p.Add("Regra da data de competência da DPS não definida.");
        if (string.IsNullOrWhiteSpace(e.InscricaoMunicipal)) p.Add("Inscrição municipal não informada (verificar se é exigida pelo município).");
        return p;
    }

    public static string NormalizarDocumento(string? doc) => DocumentoFiscal.Normalizar(doc);

    public static DadosTomador NormalizarTomador(DadosTomador t) => t with
    {
        Documento = DocumentoFiscal.Normalizar(t.Documento),
        Nome = (t.Nome ?? "").Trim(),
        Email = Formatos.Limpar(t.Email),
        Telefone = Formatos.Limpar(t.Telefone),
        InscricaoMunicipal = Formatos.Limpar(t.InscricaoMunicipal),
        Endereco = NormalizarEndereco(t.Endereco ?? new()),
    };

    public static Endereco NormalizarEndereco(Endereco e) => e with
    {
        Cep = Formatos.Limpar(Formatos.ApenasDigitos(e.Cep)),
        Logradouro = Formatos.Limpar(e.Logradouro),
        Numero = Formatos.Limpar(e.Numero),
        Complemento = Formatos.Limpar(e.Complemento),
        Bairro = Formatos.Limpar(e.Bairro),
        CodigoMunicipio = Formatos.Limpar(Formatos.ApenasDigitos(e.CodigoMunicipio)),
        NomeMunicipio = Formatos.Limpar(e.NomeMunicipio),
        Uf = Formatos.Limpar(e.Uf)?.ToUpperInvariant(),
    };

    public static DadosServico NormalizarServico(DadosServico s) => s with
    {
        Descricao = (s.Descricao ?? "").Trim(),
        CodigoTributacaoNacional = Formatos.Limpar(Formatos.ApenasDigitos(s.CodigoTributacaoNacional)),
        CodigoTributacaoMunicipal = Formatos.Limpar(s.CodigoTributacaoMunicipal),
        CodigoNbs = Formatos.Limpar(Formatos.ApenasDigitos(s.CodigoNbs)),
        CodigoMunicipioPrestacao = Formatos.Limpar(Formatos.ApenasDigitos(s.CodigoMunicipioPrestacao)),
        CstIbsCbs = Formatos.Limpar(Formatos.ApenasDigitos(s.CstIbsCbs)),
        ClassificacaoTributariaIbsCbs = Formatos.Limpar(Formatos.ApenasDigitos(s.ClassificacaoTributariaIbsCbs)),
        IndicadorOperacaoIbsCbs = Formatos.Limpar(Formatos.ApenasDigitos(s.IndicadorOperacaoIbsCbs)),
        InformacoesComplementares = Formatos.Limpar(s.InformacoesComplementares),
    };
}
