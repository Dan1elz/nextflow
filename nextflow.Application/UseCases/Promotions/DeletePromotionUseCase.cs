using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Promotions;

/// <summary>Cancela a promoção (soft delete: IsActive = false).</summary>
public class DeletePromotionUseCase(IPromotionRepository repository)
    : DeleteUseCaseBase<Promotion, IPromotionRepository>(repository)
{
    public override async Task Execute(Guid id, CancellationToken ct, Guid? userId = null)
    {
        if (!await _repository.ExistsAsync(p => p.Id == id, ct))
            throw new NotFoundException($"Promoção com id {id} não encontrada.");

        await base.Execute(id, ct, userId);
    }

    protected override void ValidateBusinessRules(Promotion entity)
    {
        if (entity.GetStatus(BusinessClock.Today) == Promotion.StatusEnded)
            throw new BadRequestException("Não é possível cancelar uma promoção já encerrada.");
    }
}
