using FluentValidation;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Unit.Core;

/// <summary>Entidade mínima para testar as bases de use case sem depender de um módulo.</summary>
public class TestEntity : BaseModel, IDeletable, IUpdatable<TestRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public override string Preposition => "o";
    public override string Singular => "registro de teste";
    public override string Plural => "registros de teste";

    public TestEntity(TestRequestDto dto) => Name = dto.Name;

    public void Update(TestRequestDto dto) => Name = dto.Name;
    public void Delete() => IsActive = false;
    public void Reactivate() => IsActive = true;
}

public class TestRequestDto
{
    public string Name { get; set; } = string.Empty;
}

public class TestRequestDtoValidator : AbstractValidator<TestRequestDto>
{
    public TestRequestDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Informe o nome.");
    }
}

public interface ITestEntityRepository : IBaseRepository<TestEntity>;
