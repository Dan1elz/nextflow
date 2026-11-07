using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Promotions;

public class GetPromotionByIdUseCase(IPromotionRepository repository, IStorageService storageService)
    : GetByIdUseCaseBase<Promotion, IPromotionRepository, PromotionResponseDto>(repository)
{
    private readonly IStorageService _storageService = storageService;

    public override async Task<PromotionResponseDto> Execute(Guid id, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct, GetInclude())
            ?? throw new NotFoundException($"Promoção com id {id} não encontrada.");

        return MapToResponseDto(entity);
    }

    protected override PromotionResponseDto MapToResponseDto(Promotion entity)
        => PromotionMappings.ToResponse(entity, _storageService);

    protected override Func<IQueryable<Promotion>, IQueryable<Promotion>>? GetInclude()
    {
        var today = BusinessClock.Today;
        return query => PromotionMappings.IncludeProduct(query, today);
    }
}
