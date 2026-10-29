namespace Nextflow.Application.Filters;

/// <summary>
/// Representa um conjunto de filtros provenientes de uma requisição (IReadOnlyDictionary).
/// Fornece métodos utilitários para extração segura de valores.
/// </summary>
public sealed class FilterSet(IReadOnlyDictionary<string, string>? filters)
{
    private readonly IReadOnlyDictionary<string, string>? _filters = filters;

    /// <summary>
    /// Indica se o conjunto de filtros está vazio ou nulo.
    /// </summary>
    public bool IsEmpty => _filters == null || _filters.Count == 0;

    /// <summary>
    /// Tenta obter o valor de uma chave específica como uma string limpa (Trim).
    /// </summary>
    /// <param name="key">A chave do filtro.</param>
    /// <param name="value">O valor resultante, ou string vazia se não encontrado.</param>
    public bool TryGetString(string key, out string value)
    {
        value = string.Empty;
        if (_filters == null) return false;
        if (!_filters.TryGetValue(key, out var raw)) return false;

        var v = raw?.Trim();
        if (string.IsNullOrWhiteSpace(v)) return false;

        value = v;
        return true;
    }
}
