namespace Nextflow.Application.UseCases.Base;

/// <summary>
/// Normaliza offset/limit vindos da query string antes de chegar ao repositório: sem teto, um
/// <c>limit=1000000</c> traria a tabela inteira (com includes); valores negativos viram 500.
/// </summary>
public static class PagingLimits
{
    public const int MaxLimit = 100;

    public static (int Offset, int Limit) Normalize(int offset, int limit)
        => (Math.Max(0, offset), Math.Clamp(limit, 1, MaxLimit));
}
