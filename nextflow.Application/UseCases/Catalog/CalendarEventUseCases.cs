using System.Linq.Expressions;
using FluentValidation;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.CalendarEvents;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Catalog;

internal static class CalendarEventRules
{
    public const string NameTakenMessage = "Já existe um evento com este nome.";
    public const string NationalLockedMessage = "Evento nacional só permite alterar a antecedência: nome e regra de data são fixos.";

    /// <summary>Nome único entre nacionais e da empresa, sem diferenciar maiúsculas (RN-CE44).</summary>
    public static Task<bool> NameTaken(ICalendarEventRepository repository, CalendarEventRequestDto dto, Guid? ignoredId, CancellationToken ct)
    {
        var name = dto.Name.Trim().ToLowerInvariant();
        return repository.Exists(e => e.Name.ToLower() == name && e.Id != ignoredId, ct);
    }
}

/// <summary>Sempre cria evento da empresa: o escopo não vem do cliente (RN-CE42).</summary>
public class CreateCalendarEventUseCase(
    ICalendarEventRepository calendarEventRepository,
    IUnitOfWork unitOfWork,
    IValidator<CalendarEventRequestDto> validator,
    IResultFactory<CalendarEventResponseDto> resultFactory,
    IBusinessClock clock)
    : CreateUseCaseBase<CalendarEvent, ICalendarEventRepository, CalendarEventRequestDto, CalendarEventResponseDto>(
        calendarEventRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<CalendarEventResponseDto>?> ValidateBusinessRules(CalendarEventRequestDto dto, CancellationToken ct)
        => await CalendarEventRules.NameTaken(_repository, dto, null, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Name), CalendarEventRules.NameTakenMessage)
            : null;

    protected override CalendarEvent MapToEntity(CalendarEventRequestDto dto) => new(dto, EventScopeEnum.Company);

    protected override CalendarEventResponseDto MapToResponseDto(CalendarEvent entity) => new(entity, clock.Today);
}

/// <summary>Evento nacional só altera a antecedência; nome ou regra diferentes do gravado devolvem 400 (RN-CE42).</summary>
public class UpdateCalendarEventUseCase(
    ICalendarEventRepository calendarEventRepository,
    IUnitOfWork unitOfWork,
    IValidator<CalendarEventRequestDto> validator,
    IResultFactory<CalendarEventResponseDto> resultFactory,
    IBusinessClock clock)
    : UpdateUseCaseBase<CalendarEvent, ICalendarEventRepository, CalendarEventRequestDto, CalendarEventResponseDto>(
        calendarEventRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<CalendarEventResponseDto>?> ValidateBusinessRules(CalendarEvent entity, CalendarEventRequestDto dto, CancellationToken ct)
    {
        if (entity.Scope == EventScopeEnum.National && !entity.HasSameDefinition(dto))
            return _resultFactory.Failure(CalendarEventRules.NationalLockedMessage, 400);

        return await CalendarEventRules.NameTaken(_repository, dto, entity.Id, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Name), CalendarEventRules.NameTakenMessage)
            : null;
    }

    protected override CalendarEventResponseDto MapToResponseDto(CalendarEvent entity) => new(entity, clock.Today);
}

/// <summary>Soft delete nos dois escopos, porque <c>product_events</c> vai apontar para o evento na F1.4 (RN-CE43).</summary>
public class DeleteCalendarEventUseCase(
    ICalendarEventRepository calendarEventRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<CalendarEvent, ICalendarEventRepository>(calendarEventRepository, unitOfWork, resultFactory);

public class ReactivateCalendarEventUseCase(
    ICalendarEventRepository calendarEventRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<CalendarEvent, ICalendarEventRepository>(calendarEventRepository, unitOfWork, resultFactory);

public class GetCalendarEventByIdUseCase(
    ICalendarEventRepository calendarEventRepository,
    IResultFactory<CalendarEventResponseDto> resultFactory,
    IBusinessClock clock)
    : GetByIdUseCaseBase<CalendarEvent, ICalendarEventRepository, CalendarEventResponseDto>(calendarEventRepository, resultFactory)
{
    protected override CalendarEventResponseDto MapToResponseDto(CalendarEvent entity) => new(entity, clock.Today);
}

/// <summary>
/// Listagem (T6). Filtros: <c>search</c> (nome), <c>isActive</c> e <c>scope</c>. Ordena por nome: <c>nextDate</c> é
/// calculado (RN-CE46) e não é ordenável no banco.
/// </summary>
public class GetAllCalendarEventsUseCase(
    ICalendarEventRepository calendarEventRepository,
    IResultFactory<PaginationDto<CalendarEventResponseDto>> resultFactory,
    IBusinessClock clock)
    : GetAllUseCaseBase<CalendarEvent, ICalendarEventRepository, CalendarEventResponseDto>(calendarEventRepository, resultFactory)
{
    protected override Expression<Func<CalendarEvent, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<CalendarEvent>()
            .WhereBoolEquals(set, "isActive", e => e.IsActive)
            .WhereEnumEquals(set, "scope", e => e.Scope);

        if (set.TryGetString("search", out var search))
        {
            var term = search.ToLowerInvariant();
            builder.And(e => e.Name.ToLower().Contains(term));
        }

        return builder.Predicate;
    }

    protected override Func<IQueryable<CalendarEvent>, IOrderedQueryable<CalendarEvent>> GetOrderBy()
        => q => q.OrderBy(e => e.Name).ThenBy(e => e.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<CalendarEvent, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<CalendarEvent, object?>>>
        {
            ["name"] = e => e.Name,
            ["leadDays"] = e => e.LeadDays,
        };

    protected override CalendarEventResponseDto MapToResponseDto(CalendarEvent entity) => new(entity, clock.Today);
}

/// <summary>Combo de evento: só ativos (usado pelo produto na F1.4).</summary>
public class GetCalendarEventOptionsUseCase(
    ICalendarEventRepository calendarEventRepository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<CalendarEvent, ICalendarEventRepository, Option>(calendarEventRepository, resultFactory)
{
    protected override Expression<Func<CalendarEvent, bool>> BuildSearch(string term) => e => e.Name.ToLower().Contains(term);

    protected override Func<IQueryable<CalendarEvent>, IOrderedQueryable<CalendarEvent>> GetOrderBy()
        => q => q.OrderBy(e => e.Name).ThenBy(e => e.Id);

    protected override Option MapToOption(CalendarEvent entity) => new(entity.Id.ToString(), entity.Name);
}
