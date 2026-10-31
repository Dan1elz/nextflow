using FluentValidation;
using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Validators.Rules;

namespace Nextflow.Domain.Validators.Suppliers;

public class SupplierRequestDtoValidator : AbstractValidator<SupplierRequestDto>
{
    public SupplierRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome.")
            .MaximumLength(150).WithMessage("O nome deve ter no máximo 150 caracteres.");
        RuleFor(x => x.Cnpj).NotEmpty().WithMessage("Informe o CNPJ.");
        RuleFor(x => x.Cnpj).ValidCnpj().When(x => !string.IsNullOrEmpty(x.Cnpj));
        RuleFor(x => x.LeadTimeDays).InclusiveBetween(1, 180).WithMessage("O prazo de entrega deve ser de 1 a 180 dias."); // RN-CD03
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("A observação deve ter no máximo 500 caracteres.");
    }
}
