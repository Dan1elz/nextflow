using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Validators.Stock;

namespace Nextflow.Tests.Unit.Stock;

/// <summary>Formato do lançamento manual (T9): sentido por motivo, custo só na abertura e observação no ajuste.</summary>
public class ManualMovementValidatorTests
{
    private readonly ManualMovementRequestDtoValidator _validator = new();

    private static ManualMovementRequestDto Dto(MovementTypeEnum type, MovementReasonEnum reason, decimal? unitCost = null, string? note = null) => new()
    {
        VariantId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        Type = type,
        Reason = reason,
        Quantity = 1,
        UnitCost = unitCost,
        Note = note,
    };

    private List<string> Fields(ManualMovementRequestDto dto) => [.. _validator.Validate(dto).Errors.Select(e => e.PropertyName)];

    [Theory]
    [Trait("Rule", "RN-CE02")]
    [InlineData(MovementTypeEnum.In, MovementReasonEnum.Opening, 5.5)]
    [InlineData(MovementTypeEnum.In, MovementReasonEnum.Adjustment, null)]
    [InlineData(MovementTypeEnum.Out, MovementReasonEnum.Adjustment, null)]
    [InlineData(MovementTypeEnum.Out, MovementReasonEnum.Breakage, null)]
    [InlineData(MovementTypeEnum.Out, MovementReasonEnum.Expired, null)]
    [InlineData(MovementTypeEnum.Out, MovementReasonEnum.Theft, null)]
    [InlineData(MovementTypeEnum.Out, MovementReasonEnum.InternalUse, null)]
    public void Should_Accept_When_ManualReasonWithItsDirection(MovementTypeEnum type, MovementReasonEnum reason, double? cost)
        => Fields(Dto(type, reason, (decimal?)cost, note: "conferência")).ShouldBeEmpty();

    [Theory]
    [Trait("Rule", "RN-CE02")]
    [InlineData(MovementReasonEnum.Purchase)]
    [InlineData(MovementReasonEnum.Sale)]
    [InlineData(MovementReasonEnum.CustomerReturn)]
    [InlineData(MovementReasonEnum.SupplierReturn)]
    [InlineData(MovementReasonEnum.TransferIn)]
    [InlineData(MovementReasonEnum.TransferOut)]
    [InlineData(MovementReasonEnum.CountAdjustment)]
    public void Should_RejectReason_When_SystemReason(MovementReasonEnum reason)
        => Fields(Dto(MovementTypeEnum.In, reason)).ShouldBe(["Reason"]);

    [Theory]
    [Trait("Rule", "L5")]
    [InlineData(MovementTypeEnum.Out, MovementReasonEnum.Opening)]
    [InlineData(MovementTypeEnum.In, MovementReasonEnum.Breakage)]
    [InlineData(MovementTypeEnum.In, MovementReasonEnum.InternalUse)]
    public void Should_RejectType_When_DirectionDoesNotMatchReason(MovementTypeEnum type, MovementReasonEnum reason)
        => Fields(Dto(type, reason)).ShouldBe(["Type"]);

    [Fact]
    [Trait("Rule", "L5")]
    public void Should_RejectUnitCost_When_NotOpening()
        => Fields(Dto(MovementTypeEnum.In, MovementReasonEnum.Adjustment, 2m, "conferência")).ShouldBe(["UnitCost"]);

    [Fact]
    [Trait("Rule", "L5")]
    public void Should_RejectUnitCost_When_OpeningCostIsZero()
        => Fields(Dto(MovementTypeEnum.In, MovementReasonEnum.Opening, 0m)).ShouldBe(["UnitCost"]);

    [Theory]
    [Trait("Rule", "L4")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_RejectNote_When_AdjustmentWithoutNote(string? note)
        => Fields(Dto(MovementTypeEnum.Out, MovementReasonEnum.Adjustment, note: note)).ShouldBe(["Note"]);

    [Fact]
    [Trait("Rule", "T9")]
    public void Should_RejectLot_When_LotInformedBeforeLotControl()
    {
        var dto = Dto(MovementTypeEnum.In, MovementReasonEnum.Opening);
        dto.LotId = Guid.NewGuid();
        dto.NewLot = new ManualMovementLotDto { Code = "L1", ExpiresOn = new DateOnly(2027, 1, 1) };

        Fields(dto).ShouldBe(["LotId", "NewLot"], ignoreOrder: true);
    }
}
