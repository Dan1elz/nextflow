namespace Nextflow.Domain.Dtos.Stock;

/// <summary>
/// Aba Lotes do T3 (F1.7): saldo de um lote num local. <see cref="DaysToExpire"/> conta do dia de negócio de hoje até a
/// validade (negativo = vencido); <see cref="IsExpiringSoon"/> marca o que vence dentro da janela de alerta.
/// </summary>
public class LotBalanceDto
{
    public Guid LotId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public int DaysToExpire { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    public Guid VariantId { get; set; }

    /// <summary>"P / Azul"; vazio na variação padrão oculta.</summary>
    public string VariantLabel { get; set; } = string.Empty;

    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
