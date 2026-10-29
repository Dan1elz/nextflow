using Nextflow.Utils;

namespace Nextflow.Tests.Unit.Core;

public class FilterHelperTests
{
    [Fact]
    public void Should_ConvertJsonValuesToTrimmedStrings_When_FiltersAreValid()
    {
        var filters = FilterHelper.Parse("""{ "search": "  Brigadeiro ", "isActive": true, "storeId": null, "page": 2 }""");

        filters.ShouldNotBeNull();
        filters["search"].ShouldBe("Brigadeiro");
        filters["isActive"].ShouldBe("True");
        filters["page"].ShouldBe("2");
        filters.ContainsKey("storeId").ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("{}")]
    public void Should_ReturnNull_When_FiltersAreEmptyOrInvalid(string? raw)
        => FilterHelper.Parse(raw).ShouldBeNull();
}
