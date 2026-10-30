using FluentValidation;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Validators.Rules;

namespace Nextflow.Domain.Validators.Auth;

public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).NotEmpty().WithMessage("Informe a senha.");
    }
}

public class UpdateProfileRequestDtoValidator : AbstractValidator<UpdateProfileRequestDto>
{
    public UpdateProfileRequestDtoValidator()
    {
        RuleFor(x => x.Name).PersonName("nome");
        RuleFor(x => x.LastName).PersonName("sobrenome");
        RuleFor(x => x.Email).ValidEmail();
    }
}

public class ChangePasswordRequestDtoValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(x => x.NewPassword).StrongPassword();
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword).WithMessage("A nova senha precisa ser diferente da atual.")
            .When(x => !string.IsNullOrEmpty(x.CurrentPassword));
    }
}
