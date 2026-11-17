using System.Globalization;

namespace HelpNfse.Core.Servicos;

/// <summary>Erro de regra de negócio, com mensagem pronta para o usuário.</summary>
public class ErroNegocio(string mensagem, int status = 400, IReadOnlyList<string>? detalhes = null) : Exception(mensagem)
{
    public int Status { get; } = status;
    public IReadOnlyList<string> Detalhes { get; } = detalhes ?? [];

    public static ErroNegocio NaoEncontrado(string oque) => new($"{oque} não encontrado(a).", 404);
    public static ErroNegocio Conflito(string mensagem) => new(mensagem, 409);
}

public static class Formatos
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Lazy<TimeZoneInfo> _fusoBrasilia = new(() =>
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasília", "Brasília"); }
    });

    public static TimeZoneInfo FusoBrasilia => _fusoBrasilia.Value;

    public static DateTimeOffset ParaBrasilia(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, FusoBrasilia);

    public static string Moeda(decimal valor) => valor.ToString("C", PtBr);

    public static string NomeMes(int mes) => PtBr.DateTimeFormat.GetMonthName(mes);

    public static string MesAno(int ano, int mes) => $"{NomeMes(mes)}/{ano}";

    public static string DataHora(DateTimeOffset utc) => ParaBrasilia(utc).ToString("dd/MM/yyyy HH:mm:ss", PtBr);

    public static string ApenasDigitos(string? s) => new((s ?? "").Where(char.IsAsciiDigit).ToArray());

    public static string? Limpar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// Substitui marcadores simples na descrição padrão de uma recorrência ao gerar a linha da competência.
/// {competencia} → 11/2026 · {mes_ano} → novembro/2026 · {mes} → novembro · {ano} → 2026 ·
/// {mes_anterior} → outubro · {competencia_anterior} → 10/2026.
/// Marcador escrito em MAIÚSCULAS gera o texto em maiúsculas (ex.: {MES_ANTERIOR} → OUTUBRO).
/// </summary>
public static partial class ModeloDescricao
{
    public static readonly string[] Marcadores =
        ["{competencia}", "{mes_ano}", "{mes}", "{ano}", "{mes_anterior}", "{competencia_anterior}"];

    [System.Text.RegularExpressions.GeneratedRegex(@"\{(competencia_anterior|competencia|mes_ano|mes_anterior|mes|ano)\}",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex Marcador();

    public static string Aplicar(string modelo, int ano, int mes)
    {
        var anterior = new DateOnly(ano, mes, 1).AddMonths(-1);
        return Marcador().Replace(modelo, m =>
        {
            var valor = m.Groups[1].Value.ToLowerInvariant() switch
            {
                "competencia" => $"{mes:00}/{ano}",
                "mes_ano" => Formatos.MesAno(ano, mes),
                "mes" => Formatos.NomeMes(mes),
                "ano" => ano.ToString(CultureInfo.InvariantCulture),
                "mes_anterior" => Formatos.NomeMes(anterior.Month),
                "competencia_anterior" => $"{anterior.Month:00}/{anterior.Year}",
                _ => m.Value,
            };
            var token = m.Groups[1].Value;
            return token == token.ToUpperInvariant() ? valor.ToUpper(Formatos.PtBr) : valor;
        });
    }
}
