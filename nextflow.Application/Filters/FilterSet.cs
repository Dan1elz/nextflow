using System.Collections.Generic;

namespace Nextflow.Application.Filters;

public sealed class FilterSet(IReadOnlyDictionary<string, string>? filters)
{
    private readonly IReadOnlyDictionary<string, string>? _filters = filters;

    public bool IsEmpty => _filters == null || _filters.Count == 0;

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

    public bool TryGetGuid(string key, out Guid guid)
    {
        guid = Guid.Empty;
        return TryGetString(key, out var raw) && FilterValueParsers.TryParseGuid(raw, out guid);
    }
}

