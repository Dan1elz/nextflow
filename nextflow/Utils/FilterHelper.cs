using System.Globalization;
using System.Text.Json;

namespace Nextflow.Utils;

/// <summary>
/// Helper utilitário para converter payloads JSON de filtros em dicionários legíveis pela camada de Application.
/// </summary>
public static class FilterHelper
{
    /// <summary>
    /// Realiza o Parsing de uma string JSON para um dicionário de strings.
    /// Ignora valores nulos ou vazios e limpa os espaços em branco.
    /// </summary>
    /// <param name="filtersJson">O JSON bruto (geralmente enviado via Query string encodeada).</param>
    /// <returns>Um dicionário IReadOnlyDictionary ou null se o JSON for inválido/vazio.</returns>
    public static IReadOnlyDictionary<string, string>? Parse(string? filtersJson)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
            return null;

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object?>>(filtersJson);
            if (raw == null || raw.Count == 0) return null;

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in raw)
            {
                if (string.IsNullOrWhiteSpace(k) || v is null) continue;

                var s = string.Empty;
                if (v is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.String)
                        s = je.GetString();
                    else
                        s = je.ToString();
                }
                else
                {
                    s = v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v.ToString();
                }

                if (string.IsNullOrWhiteSpace(s)) continue;
                result[k] = s.Trim();
            }

            return result.Count > 0 ? result : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Garante que uma chave de filtro exista no dicionário, aplicando o valor padrão quando ausente.
    /// Útil para forçar filtros obrigatórios de rota (ex: id do recurso pai).
    /// </summary>
    /// <param name="filters">O dicionário de filtros original (pode ser null).</param>
    /// <param name="key">A chave a garantir.</param>
    /// <param name="value">O valor padrão aplicado quando a chave não existe.</param>
    /// <returns>Um novo dicionário com a chave garantida.</returns>
    public static IReadOnlyDictionary<string, string>? EnsureDefault(
        IReadOnlyDictionary<string, string>? filters,
        string key,
        string value)
    {
        var dict = filters != null
            ? new Dictionary<string, string>(filters, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!dict.ContainsKey(key))
            dict[key] = value;

        return dict.Count > 0 ? dict : null;
    }
}
