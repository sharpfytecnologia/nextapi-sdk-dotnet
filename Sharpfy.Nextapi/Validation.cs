using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpfy.Nextapi;

/// <summary>Normalização e validação das entradas, antes de qualquer requisição.</summary>
public static partial class NextApiInput
{
    [GeneratedRegex("^[0-9A-Z]{12}[0-9]{2}$")] private static partial Regex Cnpj();
    [GeneratedRegex("^[0-9]{8}$")] private static partial Regex Cep();
    [GeneratedRegex("^[0-9a-fA-F]{20}$")] private static partial Regex Hash();
    [GeneratedRegex("^[0-9A-Za-z_-]+$")] private static partial Regex Segment();
    [GeneratedRegex(@"[\s./-]")] private static partial Regex CnpjPunctuation();

    /// <summary>Tira pontuação e põe em maiúsculas. Aceita o CNPJ alfanumérico (12 alfanuméricos e 2 dígitos).</summary>
    public static string NormalizeCnpj(string cnpj)
    {
        var value = CnpjPunctuation().Replace(cnpj ?? "", "").ToUpperInvariant();
        return Cnpj().IsMatch(value) ? value : throw Invalid($"CNPJ inválido: \"{cnpj}\". Informe 14 caracteres, com ou sem pontuação.");
    }

    /// <summary>CEP com 8 dígitos (aceita "01001-000").</summary>
    public static string NormalizeCep(string cep)
    {
        var value = Regex.Replace(cep ?? "", @"[\s.-]", "");
        return Cep().IsMatch(value) ? value : throw Invalid($"CEP inválido: \"{cep}\". Informe 8 dígitos.");
    }

    internal static string HashSocio(string hash)
    {
        var value = (hash ?? "").Trim();
        return Hash().IsMatch(value) ? value.ToLowerInvariant() : throw Invalid("hashSocio inválido: são 20 caracteres hexadecimais.");
    }

    internal static string PathSegment(string? value, string name)
    {
        var text = (value ?? "").Trim();
        return Segment().IsMatch(text) ? Uri.EscapeDataString(text) : throw Invalid($"{name} inválido: \"{value}\".");
    }

    internal static string PositiveInt(int value, string name) =>
        value > 0 ? value.ToString(CultureInfo.InvariantCulture) : throw Invalid($"{name} inválido: \"{value}\".");

    internal static void RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0) throw Invalid($"{name} tem de ser maior que zero.");
    }

    internal static void RequireRange(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max) throw Invalid($"{name} tem de estar entre {min} e {max}.");
    }

    internal static List<string> Batch(IEnumerable<string> cnpjs)
    {
        var list = (cnpjs ?? throw Invalid("Informe ao menos um CNPJ.")).ToList();
        if (list.Count == 0) throw Invalid("Informe ao menos um CNPJ.");
        if (list.Count > 100) throw Invalid($"O lote aceita até 100 CNPJs; vieram {list.Count}.");
        return list.Select(NormalizeCnpj).ToList();
    }

    /// <summary>Query string sem os valores ausentes, na ordem informada.</summary>
    internal static string Query(params (string Key, object? Value)[] pairs)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in pairs)
        {
            var text = value switch
            {
                null => null,
                bool b => b ? "true" : "false",
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
            if (string.IsNullOrEmpty(text)) continue;
            builder.Append(builder.Length == 0 ? '?' : '&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(text));
        }
        return builder.ToString();
    }

    private static NextApiValidationException Invalid(string message) => new(message);
}
