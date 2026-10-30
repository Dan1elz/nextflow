using FluentValidation;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Validators.Rules;

namespace Nextflow.Domain.Validators.Users;

public class UpdateUserRequestDtoValidator : AbstractValidator<UpdateUserRequestDto>
{
    public UpdateUserRequestDtoValidator(IBusinessClock clock)
    {
        RuleFor(x => x.Name).PersonName("nome");
        RuleFor(x => x.LastName).PersonName("sobrenome");
        RuleFor(x => x.BirthDate).PastBirthDate(clock);
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.NewPassword).StrongPassword().When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}
