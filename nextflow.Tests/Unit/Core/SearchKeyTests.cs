using Nextflow.Domain.Utils;

namespace Nextflow.Tests.Unit.Core;

public class SearchKeyTests
{
    [Theory]
    [InlineData("Santa Bárbara d'Oeste", "santabarbaradoeste")]
    [InlineData("SANTA BARBARA D OESTE", "santabarbaradoeste")]
    [InlineData("Pingo-d'Água", "pingodagua")]
    [InlineData("  São João del-Rei ", "saojoaodelrei")]
    [InlineData(null, "")]
    public void Should_DropAccentCaseSpacesAndPunctuation_When_BuildingKey(string? value, string expected)
        => SearchKey.From(value).ShouldBe(expected);
}
