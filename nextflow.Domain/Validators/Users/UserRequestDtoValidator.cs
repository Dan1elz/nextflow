using FluentValidation;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Validators.Rules;

namespace Nextflow.Domain.Validators.Users;

public class UserRequestDtoValidator : AbstractValidator<UserRequestDto>
{
    public UserRequestDtoValidator(IBusinessClock clock)
    {
        RuleFor(x => x.Name).PersonName("nome");
        RuleFor(x => x.LastName).PersonName("sobrenome");
        RuleFor(x => x.Cpf).NotEmpty().WithMessage("Informe o CPF.").ValidCpf();
        RuleFor(x => x.BirthDate).PastBirthDate(clock);
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).StrongPassword();
    }
}
