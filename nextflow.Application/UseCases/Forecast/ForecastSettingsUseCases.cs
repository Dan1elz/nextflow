using FluentValidation;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Forecast;

/// <summary>Agendamento da análise (T-DF11); sem nada salvo, devolve o padrão (ligado, segunda 06:00, lote de 40).</summary>
public class GetForecastSettingsUseCase(
    IForecastSettingsRepository settingsRepository,
    IAiClient aiClient,
    IBusinessClock clock,
    IResultFactory<ForecastSettingsDto> resultFactory) : IGetForecastSettingsUseCase
{
    public async Task<ResultDto<ForecastSettingsDto>> Execute(CancellationToken ct = default)
    {
        var settings = await settingsRepository.Get(_ => true, ct: ct) ?? new ForecastSettings();
        return resultFactory.Success(new ForecastSettingsDto(settings, aiClient.IsConfigured, ForecastRunReading.NextScheduledAt(settings, clock)));
    }
}

/// <summary>Salva o agendamento (T-DF11): cria a linha única na primeira vez e atualiza depois. A chave da IA não passa por aqui.</summary>
public class UpdateForecastSettingsUseCase(
    IForecastSettingsRepository settingsRepository,
    IAiClient aiClient,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<ForecastSettingsRequestDto> validator,
    IResultFactory<ForecastSettingsDto> resultFactory) : IUpdateForecastSettingsUseCase
{
    public async Task<ResultDto<ForecastSettingsDto>> Execute(ForecastSettingsRequestDto dto, Guid? userId, CancellationToken ct = default)
    {
        if (userId is null)
            return resultFactory.Failure("Usuário não identificado.", 401);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation);

        var existing = (await settingsRepository.GetAllForUpdate(_ => true, ct)).FirstOrDefault();
        ForecastSettings settings;
        if (existing is null)
        {
            settings = new ForecastSettings(dto, userId.Value, clock.UtcNow);
            await settingsRepository.AddAsync(settings, ct);
        }
        else
        {
            existing.Update(dto, userId.Value, clock.UtcNow);
            settings = existing;
        }

        await unitOfWork.CommitAsync(ct);
        return resultFactory.Success(new ForecastSettingsDto(settings, aiClient.IsConfigured, ForecastRunReading.NextScheduledAt(settings, clock)));
    }
}
