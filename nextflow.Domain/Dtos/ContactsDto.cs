using Nextflow.Domain.Dtos.Base;
using System.ComponentModel.DataAnnotations;
using Nextflow.Domain.Attributes;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos;

public class CreateContactDto : BaseDto, IValidatableObject
{
    public Guid? ClientId { get; set; }
    public Guid? SupplierId { get; set; }

    [StringLength(100, MinimumLength = 2, ErrorMessage = "A Descrição do contato deve ter no máximo 100 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "A descrição do contato é obrigatório.")]
    public string Description { get; set; } = string.Empty;

    private string? _fone;
    [StringLength(15, ErrorMessage = "O telefone deve ter no máximo 15 caracteres.")]
    public string? Fone
    {
        get => _fone;
        set => _fone = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private string? _email;
    [EmailAddress(ErrorMessage = "Formato de e-mail inválido.")]
    public string? Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Fone) && string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult(
                "Pelo menos um dos campos (Telefone ou E-mail) deve ser preenchido.",
                new[] { nameof(Fone), nameof(Email) }
            );
        }
    }
}

public class UpdateContactDto : CreateContactDto { }

public class ContactResponseDto : BaseDto
{
    public Guid Id { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? SupplierId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Fone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public ContactResponseDto() { }

    public ContactResponseDto(Contact entity)
    {
        Id = entity.Id;
        ClientId = entity.ClientId;
        SupplierId = entity.SupplierId;
        Description = entity.Description;
        Fone = entity.Fone ?? string.Empty;
        Email = entity.Email ?? string.Empty;
    }
}
