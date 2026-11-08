using Nextflow.Application.Filters;
using Nextflow.Domain.Enums;

namespace Nextflow.Tests.Unit.Core;

public class FilterExpressionBuilderTests
{
    private sealed record Row(PurchaseStatusEnum Status);

    private static readonly Row[] Rows = Enum.GetValues<PurchaseStatusEnum>().Select(status => new Row(status)).ToArray();

    private static List<PurchaseStatusEnum> Filter(string raw)
    {
        var predicate = new FilterExpressionBuilder<Row>()
            .WhereEnumEquals(new FilterSet(new Dictionary<string, string> { ["status"] = raw }), "status", r => r.Status)
            .Predicate.Compile();
        return Rows.Where(predicate).Select(r => r.Status).ToList();
    }

    [Fact]
    public void Should_FilterBySingleStatus_When_OneValueIsGiven()
        => Filter("Pending").ShouldBe([PurchaseStatusEnum.Pending]);

    [Fact]
    public void Should_FilterByAnyListedStatus_When_ValuesAreCommaSeparated()
        => Filter("Budget, Pending").ShouldBe([PurchaseStatusEnum.Budget, PurchaseStatusEnum.Pending], ignoreOrder: true);

    [Fact]
    public void Should_IgnoreInvalidValues_When_ListMixesValidAndInvalid()
        => Filter("Budget,Nada,99").ShouldBe([PurchaseStatusEnum.Budget]);

    [Fact]
    public void Should_NotFilter_When_NoValueIsValid()
        => Filter("Nada").Count.ShouldBe(Rows.Length);
}
