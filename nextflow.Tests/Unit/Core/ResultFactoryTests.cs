using FluentValidation.Results;
using Nextflow.Application.Services;
using Nextflow.Domain.Dtos;

namespace Nextflow.Tests.Unit.Core;

public class ResultFactoryTests
{
    private readonly ResultFactory<string> _factory = new();

    [Fact]
    public void Should_GroupErrorsByCamelCaseField_When_ValidationFails()
    {
        var validation = new ValidationResult(
        [
            new ValidationFailure("Email", "Informe o e-mail."),
            new ValidationFailure("Email", "E-mail inválido."),
            new ValidationFailure("Items[0].UnitCost", "O custo não pode ser negativo."),
        ]);

        var result = _factory.Failure(validation);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["email"].ShouldBe(["Informe o e-mail.", "E-mail inválido."]);
        result.FieldErrors!["items[0].unitCost"].ShouldBe(["O custo não pode ser negativo."]);
        result.Errors.Count.ShouldBe(3);
    }

    [Fact]
    [Trait("Rule", "RN-VD13")]
    public void Should_Return422WithConfirmKey_When_WarningNeedsConfirmation()
    {
        var result = _factory.Confirm("A promoção deixa o preço abaixo do custo médio. Deseja continuar?");

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.Keys.ShouldBe([ResultDto<string>.ConfirmKey]);
        result.Message.ShouldBe("A promoção deixa o preço abaixo do custo médio. Deseja continuar?");
    }

    [Fact]
    public void Should_KeepFieldErrorsNull_When_FailureIsPlainMessage()
    {
        var result = _factory.Failure("Estoque insuficiente.", 400);

        result.FieldErrors.ShouldBeNull();
        result.Message.ShouldBe("Estoque insuficiente.");
    }
}
