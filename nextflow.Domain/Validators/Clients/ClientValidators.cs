using FluentValidation;
using Nextflow.Domain.Dtos.Clients;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Validators.Rules;

namespace Nextflow.Domain.Validators.Clients;

public class ClientRequestDtoValidator : AbstractValidator<ClientRequestDto>
{
    public ClientRequestDtoValidator(IBusinessClock clock)
    {
        RuleFor(x => x.Name).PersonName("nome");
        RuleFor(x => x.LastName).MaximumLength(100).WithMessage("O sobrenome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Cpf).ValidCpf().When(x => !string.IsNullOrWhiteSpace(x.Cpf)); // L-CP6: opcional
        RuleFor(x => x.BirthDate)
            .Must(date => date < clock.Today).WithMessage("A data de nascimento precisa estar no passado.")
            .When(x => x.BirthDate.HasValue);
    }
}

public class QuickCreateClientRequestDtoValidator : AbstractValidator<QuickCreateClientRequestDto>
{
    public QuickCreateClientRequestDtoValidator()
    {
        RuleFor(x => x.Name).PersonName("nome");
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Informe o telefone.");
        RuleFor(x => x.Phone).ValidPhone().When(x => !string.IsNullOrEmpty(x.Phone));
    }
}
