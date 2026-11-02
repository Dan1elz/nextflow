using FluentValidation;
using FluentValidation.Results;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.UseCases.Inventory;

/// <summary>
/// Lançamento manual (T9): valida o formato, entrega uma linha ao <see cref="IStockLedger"/> como documento
/// <see cref="SourceTypeEnum.Manual"/> e commita. As falhas do livro-razão (local fechado, negativo, RN-CE32) voltam no campo
/// do formulário. Não há exclusão: correção é um lançamento contrário (RN-CE14).
/// </summary>
public class CreateManualMovementUseCase(
    IStockLedger ledger,
    IUnitOfWork unitOfWork,
    IValidator<ManualMovementRequestDto> validator,
    IResultFactory<bool> resultFactory) : ICreateManualMovementUseCase
{
    private const string LinePrefix = "Lines[0].";

    public async Task<ResultDto<bool>> Execute(ManualMovementRequestDto? dto, Guid userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation, 422);

        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = userId, Note = dto.Note };
        var line = new StockLineDto
        {
            VariantId = dto.VariantId,
            LocationId = dto.LocationId,
            Type = dto.Type,
            Reason = dto.Reason,
            Quantity = dto.Quantity,
            UnitCost = dto.UnitCost,
        };

        var failures = await ledger.Post(document, [line], ct);
        if (failures.Count > 0)
            return resultFactory.Failure(new ValidationResult(failures.Select(ToFormField)), 422);

        await unitOfWork.CommitAsync(ct);
        return resultFactory.Success(true);
    }

    /// <summary>"Lines[0].Quantity" do livro-razão vira "quantity", o campo do drawer.</summary>
    private static ValidationFailure ToFormField(ValidationFailure failure)
        => new(failure.PropertyName.StartsWith(LinePrefix) ? failure.PropertyName[LinePrefix.Length..] : failure.PropertyName, failure.ErrorMessage);
}
