using FluentValidation;
using FluentValidation.Results;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

/// <summary>
/// Transferência de estoque (RN-CE62), sem commit. A saída vai primeiro (o livro-razão reparte por FEFO o que tem lote) e a
/// entrada repete as linhas lançadas no destino, nos mesmos lotes e sem custo, então o custo médio da variação não muda.
/// Falha da saída volta em <c>items[i].*</c> (ou <c>fromLocationId</c>); da entrada, em <c>toLocationId</c>.
/// </summary>
public class StockTransferService(
    IStockLedger ledger,
    IStockTransferRepository transferRepository,
    IValidator<StockTransferRequestDto> validator) : IStockTransferService
{
    public async Task<(StockTransfer? Transfer, List<ValidationFailure> Failures)> Transfer(
        StockTransferRequestDto dto, Guid userId, Guid? forecastRecommendationId, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return (null, [.. validation.Errors.Select(e => new ValidationFailure(Camel(e.PropertyName), e.ErrorMessage))]);

        var transfer = new StockTransfer(dto.FromLocationId, dto.ToLocationId, dto.Note, userId, forecastRecommendationId,
            dto.Items.Select(i => (i.VariantId, i.Quantity)));
        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Transfer, SourceId = transfer.Id, UserId = userId, Note = transfer.Note };

        var outbound = dto.Items.Select(i => new StockLineDto
        {
            VariantId = i.VariantId,
            LocationId = dto.FromLocationId,
            Type = MovementTypeEnum.Out,
            Reason = MovementReasonEnum.TransferOut,
            Quantity = i.Quantity,
        }).ToList();
        var (outFailures, posted) = await ledger.PostResolved(document, outbound, ct);
        if (outFailures.Count > 0)
            return (null, [.. outFailures.Select(f => Map(f, "fromLocationId"))]);

        var inbound = posted.Select(line => new StockLineDto
        {
            VariantId = line.VariantId,
            LocationId = dto.ToLocationId,
            LotId = line.LotId,
            Type = MovementTypeEnum.In,
            Reason = MovementReasonEnum.TransferIn,
            Quantity = line.Quantity,
        }).ToList();
        var inFailures = await ledger.Post(document, inbound, ct);
        if (inFailures.Count > 0)
            return (null, [.. inFailures.Select(f => Map(f, "toLocationId", byItem: false))]);

        await transferRepository.AddAsync(transfer, ct);
        return (transfer, []);
    }

    /// <summary>"Lines[i].Quantity" → "items[i].quantity"; o local da linha vira o campo do local do pedido.</summary>
    private static ValidationFailure Map(ValidationFailure failure, string locationField, bool byItem = true)
    {
        var name = failure.PropertyName;
        if (name.EndsWith(".LocationId", StringComparison.Ordinal) || !byItem)
            return new ValidationFailure(locationField, failure.ErrorMessage);
        return new ValidationFailure(Camel(name.Replace("Lines[", "Items[")), failure.ErrorMessage);
    }

    private static string Camel(string path)
        => string.Join('.', path.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
