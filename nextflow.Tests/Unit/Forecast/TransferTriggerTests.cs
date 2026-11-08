using Nextflow.Application.Forecast;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Gatilho do "Transferir" (RN-DF16, F3.4b).</summary>
public class TransferTriggerTests
{
    private static readonly Guid Variant = Guid.NewGuid();
    private static readonly Guid Centro = Guid.NewGuid();
    private static readonly Guid Shopping = Guid.NewGuid();
    private static readonly Guid Deposito = Guid.NewGuid();

    [Fact]
    [Trait("Rule", "RN-DF16")]
    public void Should_SuggestTransfer_When_OneLocationLacksAndOtherHasExcess()
    {
        // Shopping: 5 de mínimo 20 (falta 15). Centro: 100, mínimo 20, demanda 1/dia × 30 dias = guarda 30 → excesso 70.
        var plan = TransferTrigger.Evaluate(
        [
            new(Variant, Centro, 100m, 20m, 1m),
            new(Variant, Shopping, 5m, 20m, 1m),
        ], 30, 0);

        plan.ShouldNotBeNull();
        plan.FromLocationId.ShouldBe(Centro);
        plan.ToLocationId.ShouldBe(Shopping);
        plan.Items.ShouldBe([(Variant, 15m)]);
    }

    [Fact]
    [Trait("Rule", "RN-DF16")]
    public void Should_NotSuggest_When_NoLocationHasExcess()
    {
        // Centro tem 35 para guardar 30 dias × 1,5 = 45: nenhum excesso para mandar ao Shopping.
        var plan = TransferTrigger.Evaluate(
        [
            new(Variant, Centro, 35m, 20m, 1.5m),
            new(Variant, Shopping, 5m, 20m, 1m),
        ], 30, 0);

        plan.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF16")]
    public void Should_CapAtExcess_When_ShortfallIsBigger()
    {
        // Depósito sem demanda: excesso = 12 − mínimo 4 = 8; falta do Shopping é 20 → transfere só 8.
        var plan = TransferTrigger.Evaluate(
        [
            new(Variant, Deposito, 12m, 4m, 0m),
            new(Variant, Shopping, 0m, 20m, 2m),
        ], 30, 0);

        plan.ShouldNotBeNull();
        plan.FromLocationId.ShouldBe(Deposito);
        plan.Items.ShouldBe([(Variant, 8m)]);
    }

    [Fact]
    public void Should_RoundDown_When_UnitHasNoDecimals()
    {
        var plan = TransferTrigger.Evaluate(
        [
            new(Variant, Centro, 50.7m, 0m, 0m),
            new(Variant, Shopping, 1m, 10.5m, 0m),
        ], 30, 0);

        plan!.Items.ShouldBe([(Variant, 9m)]);
    }
}
