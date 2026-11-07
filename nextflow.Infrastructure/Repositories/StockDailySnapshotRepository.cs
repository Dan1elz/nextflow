using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

// Tabela derivada (F3.1): recriável a partir de vendas e livro-razão, então o recálculo a esvazia com um DELETE em massa, dentro da
// transação do `IUnitOfWork.ExecuteInTransactionAsync`. É a exceção declarada a "repositório só registra".
public class StockDailySnapshotRepository(AppDbContext context) : BaseRepository<StockDailySnapshot>(context), IStockDailySnapshotRepository
{
    public Task DeleteAllAsync(CancellationToken ct = default) => _context.Set<StockDailySnapshot>().ExecuteDeleteAsync(ct);
}
