using FluentValidation;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Collections;

namespace Nextflow.Domain.Validators.Catalog;

public class CategoryRequestDtoValidator : AbstractValidator<CategoryRequestDto>
{
    public CategoryRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }
}

public class CollectionRequestDtoValidator : AbstractValidator<CollectionRequestDto>
{
    public CollectionRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.EndsOn) // RN-CE40
            .GreaterThanOrEqualTo(x => x.StartsOn).WithMessage("O fim não pode ser antes do início.")
            .When(x => x.StartsOn.HasValue && x.EndsOn.HasValue);
    }
}
