using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Seeders;

/// <summary>
/// As três políticas A, B e C (T13, RN-CE29), em todo ambiente. Só cria a classe que falta: nunca sobrescreve o que o
/// Admin ajustou. A classe A gira mais e não pode faltar: cobertura curta e segurança maior; a C, o contrário.
/// </summary>
public class StockPoliciesSeeder(IStockPolicyRepository policyRepository, IUnitOfWork unitOfWork) : ISeeder
{
    private static readonly (AbcClassEnum AbcClass, int TargetCoverageDays, int SafetyDays)[] Defaults =
    [
        (AbcClassEnum.A, 15, 7),
        (AbcClassEnum.B, 30, 5),
        (AbcClassEnum.C, 45, 3),
    ];

    public int Order => 40;

    public async Task SeedAsync(CancellationToken ct)
    {
        var added = false;

        foreach (var (abcClass, coverage, safety) in Defaults)
        {
            if (await policyRepository.Exists(p => p.AbcClass == abcClass, ct))
                continue;

            await policyRepository.AddAsync(new StockPolicy(abcClass, new StockPolicyRequestDto { TargetCoverageDays = coverage, SafetyDays = safety }), ct);
            added = true;
        }

        if (added)
            await unitOfWork.CommitAsync(ct);
    }
}
