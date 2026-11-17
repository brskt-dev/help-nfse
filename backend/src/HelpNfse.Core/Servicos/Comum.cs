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
/// {competencia} → 11/2026, {mes_ano} → novembro/2026, {mes} → novembro, {ano} → 2026.
/// </summary>
public static class ModeloDescricao
{
    public static readonly string[] Marcadores = ["{competencia}", "{mes_ano}", "{mes}", "{ano}"];

    public static string Aplicar(string modelo, int ano, int mes) => modelo
        .Replace("{competencia}", $"{mes:00}/{ano}", StringComparison.OrdinalIgnoreCase)
        .Replace("{mes_ano}", Formatos.MesAno(ano, mes), StringComparison.OrdinalIgnoreCase)
        .Replace("{mes}", Formatos.NomeMes(mes), StringComparison.OrdinalIgnoreCase)
        .Replace("{ano}", ano.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
}
