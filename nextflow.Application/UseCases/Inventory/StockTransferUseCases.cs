using FluentValidation.Results;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.UseCases.Inventory;

/// <summary>Transferência manual entre locais (RN-CE62): saída e entrada no mesmo commit.</summary>
public class CreateStockTransferUseCase(
    IStockTransferService transferService,
    IStockTransferRepository transferRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<StockTransferDto> resultFactory) : ICreateStockTransferUseCase
{
    public async Task<ResultDto<StockTransferDto>> Execute(StockTransferRequestDto? dto, Guid userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var (transfer, failures) = await transferService.Transfer(dto, userId, null, ct);
        if (transfer is null)
            return resultFactory.Failure(new ValidationResult(failures), 422);

        await unitOfWork.CommitAsync(ct);
        return resultFactory.Success(new StockTransferDto((await transferRepository.GetDetail(transfer.Id, ct))!));
    }
}

public class GetStockTransferUseCase(
    IStockTransferRepository transferRepository,
    IResultFactory<StockTransferDto> resultFactory) : IGetStockTransferUseCase
{
    public async Task<ResultDto<StockTransferDto>> Execute(Guid id, CancellationToken ct = default)
        => await transferRepository.GetDetail(id, ct) is { } transfer
            ? resultFactory.Success(new StockTransferDto(transfer))
            : resultFactory.Failure("Transferência não encontrada.", 404);
}
