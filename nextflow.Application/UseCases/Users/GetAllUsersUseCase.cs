using System.Linq.Expressions;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Users;

/// <summary>Listagem de usuários (T-AC2). Filtros: <c>search</c> (nome, sobrenome, e-mail), <c>isActive</c>.</summary>
public class GetAllUsersUseCase(
    IUserRepository userRepository,
    IResultFactory<PaginationDto<UserListItemDto>> resultFactory)
    : GetAllUseCaseBase<User, IUserRepository, UserListItemDto>(userRepository, resultFactory)
{
    protected override UserListItemDto MapToResponseDto(User entity) => new(entity);

    protected override Expression<Func<User, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        return new FilterExpressionBuilder<User>()
            .WhereStringContainsAny(set, "search", u => u.Name, u => u.LastName, u => u.Email)
            .WhereBoolEquals(set, "isActive", u => u.IsActive)
            .Predicate;
    }

    protected override Func<IQueryable<User>, IOrderedQueryable<User>>? GetOrderBy()
        => q => q.OrderBy(u => u.Name).ThenBy(u => u.LastName).ThenBy(u => u.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<User, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<User, object?>>>
        {
            ["fullName"] = u => u.Name,
            ["email"] = u => u.Email,
            ["createdAt"] = u => u.CreatedAt,
            ["lastLoginAt"] = u => u.LastLoginAt,
        };
}
