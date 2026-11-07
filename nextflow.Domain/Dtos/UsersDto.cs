using System.ComponentModel.DataAnnotations;
using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos;

public class CreateUserDto : CreatePersonDto
{
    [StringLength(150, MinimumLength = 5, ErrorMessage = "O Email deve ter no máximo 150 caracteres e no mínimo 5 caracteres."), Required(ErrorMessage = "O Email é obrigatório."), EmailAddress(ErrorMessage = "O Email informado não é válido.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(32, MinimumLength = 6, ErrorMessage = "A Senha deve ter no máximo 32 caracteres e no mínimo 6 caracteres."), Required(ErrorMessage = "A Senha é obrigatória.")]
    public string Password { get; set; } = string.Empty;
}

public class UpdateUserDto : UpdatePersonDto
{
    [StringLength(150, MinimumLength = 5, ErrorMessage = "O Email deve ter no máximo 150 caracteres e no mínimo 5 caracteres."), Required(ErrorMessage = "O Email é obrigatório."), EmailAddress(ErrorMessage = "O Email informado não é válido.")]
    public string Email { get; set; } = string.Empty;
}

public class UpdatePasswordDto : BaseDto
{
    [StringLength(32, MinimumLength = 6, ErrorMessage = "A Senha deve ter no máximo 32 caracteres e no mínimo 6 caracteres."), Required(ErrorMessage = "A Senha é obrigatória.")]
    public string Password { get; set; } = string.Empty;
}

public class UserResponseDto : PersonResponseDto
{
    public string Email { get; set; } = string.Empty;
    public RoleEnum Role { get; set; }
    public UserResponseDto() : base() { }
    public UserResponseDto(User dto) : base()
    {
        Id = dto.Id;
        Name = dto.Name;
        LastName = dto.LastName;
        Email = dto.Email;
        Role = dto.Role;
        CPF = dto.CPF;
        BirthDate = dto.BirthDate;
        IsActive = dto.IsActive;
    }
}

public class LoginDto : BaseDto
{
    [StringLength(150, MinimumLength = 5, ErrorMessage = "O Email deve ter no máximo 150 caracteres e no mínimo 5 caracteres."), Required(ErrorMessage = "O Email é obrigatório."), EmailAddress(ErrorMessage = "O Email informado não é válido.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(32, MinimumLength = 6, ErrorMessage = "A Senha deve ter no máximo 32 caracteres e no mínimo 6 caracteres."), Required(ErrorMessage = "A Senha é obrigatória.")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; } = Guid.Empty;
    public UserResponseDto User { get; set; } = new UserResponseDto();
}
