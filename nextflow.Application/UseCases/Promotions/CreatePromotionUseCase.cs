using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Application.Utils;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Promotions;

public class CreatePromotionUseCase(
    IPromotionRepository repository,
    IProductRepository productRepository,
    IStorageService storageService
)
    : CreateUseCaseBase<Promotion, IPromotionRepository, CreatePromotionDto, PromotionResponseDto>(repository), ICreatePromotionUseCase
{
    public const int MaxPeriodDays = 90;

    private readonly IProductRepository _productRepository = productRepository;
    private readonly IStorageService _storageService = storageService;

    protected override Promotion MapToEntity(CreatePromotionDto dto) => new(dto);

    protected override PromotionResponseDto MapToResponseDto(Promotion entity)
        => PromotionMappings.ToResponse(entity, _storageService);

    protected override async Task ValidateBusinessRules(CreatePromotionDto dto, CancellationToken ct)
    {
        var today = BusinessClock.Today;

        if (dto.EndDate < dto.StartDate)
            throw new BadRequestException("A data de término deve ser maior ou igual à data de início.");

        if (dto.StartDate < today)
            throw new BadRequestException($"A data de início não pode ser anterior a hoje ({today:dd/MM/yyyy}).");

        var periodDays = dto.EndDate.DayNumber - dto.StartDate.DayNumber + 1;
        if (periodDays > MaxPeriodDays)
            throw new BadRequestException($"O período da promoção não pode exceder {MaxPeriodDays} dias.");

        if (dto.Source == PromotionSource.AiRecommendation && (dto.ForecastRecommendationId == null || dto.ForecastRecommendationId == Guid.Empty))
            throw new BadRequestException("Promoções geradas a partir de recomendação devem informar a recomendação de origem.");

        var product = await _productRepository.GetByIdAsync(dto.ProductId, ct, query => query
                .Include(p => p.Supplier)
                .Include(p => p.CategoryProducts)
                    .ThenInclude(cp => cp.Category)
                .IncludeCurrentPromotions(today))
            ?? throw new NotFoundException("Produto não encontrado.");

        if (!product.IsActive)
            throw new BadRequestException("Não é possível criar uma promoção para um produto inativo.");

        var overlapping = await _repository.GetAsync(p =>
            p.ProductId == dto.ProductId &&
            p.IsActive &&
            p.StartDate <= dto.EndDate &&
            p.EndDate >= dto.StartDate, ct);

        if (overlapping != null)
            throw new BadRequestException(
                $"Já existe uma promoção ativa para este produto que se sobrepõe ao período informado ({overlapping.StartDate:dd/MM/yyyy} a {overlapping.EndDate:dd/MM/yyyy}).");
    }
}
