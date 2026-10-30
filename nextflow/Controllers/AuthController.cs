using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Application.UseCases;

namespace Nextflow.Controllers;

[Authorize]
[Route("api/auth")]
public class AuthController(
    ILoginUseCase loginUseCase,
    IGetCurrentUserUseCase getCurrentUserUseCase,
    IUpdateProfileUseCase updateProfileUseCase,
    IChangePasswordUseCase changePasswordUseCase) : ApiController
{
    // Anônimo de propósito: é aqui que o usuário obtém o token.
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
        => Handle(await loginUseCase.Execute(dto, ct));

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => Handle(await getCurrentUserUseCase.Execute(UserId, ct));

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequestDto dto, CancellationToken ct)
        => Handle(await updateProfileUseCase.Execute(UserId, dto, ct));

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto, CancellationToken ct)
        => HandleNoContent(await changePasswordUseCase.Execute(UserId, dto, ct));
}
