using FluentValidation;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Validators.Rules;

namespace Nextflow.Domain.Validators.Contacts;

public class UpdateContactRequestDtoValidator : AbstractValidator<UpdateContactRequestDto>
{
    public UpdateContactRequestDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Informe o tipo: Phone, Email ou WhatsApp.");
        RuleFor(x => x.Value).NotEmpty().WithMessage("Informe o contato.");
        RuleFor(x => x.Value).ValidEmail().When(x => x.Type == ContactTypeEnum.Email && !string.IsNullOrEmpty(x.Value));
        RuleFor(x => x.Value).ValidPhone().When(x => x.Type != ContactTypeEnum.Email && !string.IsNullOrEmpty(x.Value));
    }
}

public class ContactRequestDtoValidator : AbstractValidator<ContactRequestDto>
{
    public ContactRequestDtoValidator()
    {
        Include(new UpdateContactRequestDtoValidator());
        RuleFor(x => x.OwnerType).IsInEnum().WithMessage("Informe o tipo do dono: Client ou Supplier.");
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Informe o dono do contato.");
    }
}
