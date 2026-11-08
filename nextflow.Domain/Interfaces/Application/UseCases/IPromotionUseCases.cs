using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Promotions;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

/// <summary>Prévia do T-VD7: preço promocional, custo médio e margem por variação do escopo.</summary>
public interface IGetPromotionPreviewUseCase
{
    Task<ResultDto<PromotionPreviewDto>> Execute(Guid productId, Guid? variantId, decimal discountPercent, CancellationToken ct = default);
}

/// <summary>Edita a promoção agendada (ainda não começou): escopo, desconto, período e motivo, com as regras da criação.</summary>
public interface IUpdatePromotionUseCase
{
    Task<ResultDto<PromotionListItemDto>> Execute(Guid id, PromotionRequestDto dto, CancellationToken ct = default);
}

/// <summary>Cancela (inativa) a promoção agendada ou vigente (RN-VD12).</summary>
public interface ICancelPromotionUseCase
{
    Task<ResultDto<bool>> Execute(Guid id, CancellationToken ct = default);
}
