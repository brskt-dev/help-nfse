namespace HelpNfse.Core.Validacao;

/// <summary>
/// Validação de CPF e CNPJ por dígitos verificadores. Suporta o CNPJ alfanumérico
/// (12 posições alfanuméricas + 2 dígitos verificadores numéricos).
/// </summary>
public static class DocumentoFiscal
{
    public static string Normalizar(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento)) return "";
        Span<char> buffer = stackalloc char[documento.Length];
        var n = 0;
        foreach (var c in documento)
        {
            if (char.IsAsciiLetterOrDigit(c)) buffer[n++] = char.ToUpperInvariant(c);
        }
        return new string(buffer[..n]);
    }

    public static bool EhCpf(string documentoNormalizado) => documentoNormalizado.Length == 11;
    public static bool EhCnpj(string documentoNormalizado) => documentoNormalizado.Length == 14;

    public static bool Valido(string? documento)
    {
        var d = Normalizar(documento);
        return d.Length switch
        {
            11 => CpfValido(d),
            14 => CnpjValido(d),
            _ => false,
        };
    }

    public static bool CpfValido(string? cpf)
    {
        var d = Normalizar(cpf);
        if (d.Length != 11 || !d.All(char.IsAsciiDigit)) return false;
        if (d.Distinct().Count() == 1) return false;

        int Dv(int tamanho)
        {
            var soma = 0;
            for (var i = 0; i < tamanho; i++) soma += (d[i] - '0') * (tamanho + 1 - i);
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        return Dv(9) == d[9] - '0' && Dv(10) == d[10] - '0';
    }

    public static bool CnpjValido(string? cnpj)
    {
        var d = Normalizar(cnpj);
        if (d.Length != 14) return false;
        if (!d[..12].All(char.IsAsciiLetterOrDigit) || !d[12..].All(char.IsAsciiDigit)) return false;
        if (d.Distinct().Count() == 1) return false;

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        int Dv(int[] pesos)
        {
            var soma = 0;
            for (var i = 0; i < pesos.Length; i++) soma += (d[i] - '0') * pesos[i];
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        return Dv(pesos1) == d[12] - '0' && Dv(pesos2) == d[13] - '0';
    }

    public static string Formatar(string? documento)
    {
        var d = Normalizar(documento);
        return d.Length switch
        {
            11 => $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
            14 => $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
            _ => documento ?? "",
        };
    }
}
