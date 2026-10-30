namespace Nextflow.Domain.ValueObjects;

/// <summary>
/// Base para Value Objects de domínio. Igualdade é determinada pelos componentes
/// retornados por <see cref="GetEqualityComponents"/>, não por identidade de referência.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Retorna os valores que definem a identidade deste VO.
    /// Cada propriedade semanticamente relevante deve ser incluída.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        return GetEqualityComponents().SequenceEqual(((ValueObject)obj).GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(0, (h, c) => HashCode.Combine(h, c?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? left, ValueObject? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
